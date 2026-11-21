# World View — ME-01-01 Map Editor Overall Architecture

- Version: 0.1
- Date: 2026-11-21
- Status: Architecture Working Baseline — recommended structure; detailed domain contracts and implementation choices remain open
- Scope: Unified map-editing platform, professional editing modes, shared spatial/object/presentation architecture, and boundaries with Core Domain, AI Assistant, and Map Engine
- Product target: Single-user, local-first Windows/macOS desktop application; C# + Avalonia
- Related documents: ME-01-02 World Knowledge Model & Story-to-Spatial Bridge; ME-01-03 Guided Initialization Specification; Core Domain Architecture Baseline V1.0; Core Domain P2-09 logical persistence model; AI Assistant Task Planning & Execution Engine V1.0 integration baseline

## 1. Executive recommendation

Build **one unified Map Editor platform with multiple professional editing modes**, backed by one shared spatial model and one authoritative Core Domain.

The professional modes are not separate applications, independent map files, or parallel domain models. They are task-oriented work surfaces over the same Map, Objects, Geometry, Relationships, Historical State, and spatial assets.

Initial professional modes:

1. **Terrain & Natural Geography** — elevation, landforms, mountain systems, coastlines, rivers, lakes, watersheds, climate, and ecology.
2. **Settlements & Urban Geography** — cities, towns, villages, districts, land use, roads, routes, and settlement relationships.
3. **Buildings & Interior Spaces** — buildings, floors, rooms, entrances, vertical circulation, caves, underground spaces, and connected facilities.

A fourth cross-cutting capability is not a separate professional editor: **World / Region Authoring and Guided Initialization**. It uses the shared editor shell, spatial model, World Knowledge bridge, and AI Assistant to establish the initial world skeleton and progressively refine selected regions.

The architecture has five principal layers:

- **Editor Shell** — application-level editing workspace, navigation, tool hosting, panels, selection, command routing, and user interaction.
- **Editing Modes & Tools** — terrain, settlement, building/interior, and cross-cutting authoring workflows.
- **Shared Spatial & Presentation Model** — coordinate spaces, spatial scopes, geometry, object references, layers, symbols, styles, and view projections.
- **Core Domain** — formal Map/Object identity, initial state, geometry and semantic relationships, history, Commands, Events, State Changes, Branches, and reconstruction.
- **Map Engine Services** — rendering, geometry operations, spatial algorithms, topology, terrain/hydrology/climate calculations, asset processing, and view projection support.

AI Assistant is a cooperating application subsystem rather than a layer that owns map data. It interprets creator intent, plans work, generates and validates Candidates, requests authorization, and submits typed changes through Core Domain Domain Services.

## 2. Goals and non-goals

### 2.1 Goals

- Provide one coherent editing experience across world, region, locality, and site scales.
- Allow creators to begin with a story premise, an incomplete world description, a rough sketch, or an existing map.
- Let creators move naturally from macro world creation to detailed regional, settlement, building, and interior work.
- Keep semantic Objects and their identities stable while allowing geometry and presentation to evolve.
- Allow one formal spatial model to support natural-geography and fantasy-cartography presentations.
- Support AI-assisted interpretation and generation without allowing LLM output to bypass validation, authorization, or Core Domain ownership.
- Preserve historical reconstruction and Branch semantics; map editing must produce valid formal domain changes rather than mutate a disposable current-state document.
- Keep the architecture understandable and maintainable for a single-user local-first product.

### 2.2 Non-goals for V1

- Separate standalone applications for terrain, cities, and buildings.
- A generic GIS desktop clone or a complete professional GIS toolset.
- A fully general CAD/BIM authoring platform.
- Independent object databases or geometry authorities per editor mode.
- A generic plugin/workflow framework before concrete extension needs exist.
- Distributed rendering, server-side map editing, multi-user collaboration, or cloud synchronization.
- Full Earth-system simulation or physically exhaustive climate modeling.
- Replacing Core Domain history with document snapshots, editor autosave history, or AI-generated revision chains.
- Requiring the creator to understand GIS terminology or manually specify specialist numerical parameters for ordinary creation tasks.

## 3. Product structure: one editor, several modes

### 3.1 Unified editor shell

The Map Editor opens a selected Map or Branch and provides a stable shell:

- **Main Canvas** — the primary spatial work surface.
- **Mode Selector** — switches between Terrain, Settlements, Buildings/Interiors, and general map authoring contexts.
- **Tool Shelf** — tools available in the current mode, plus shared selection/navigation tools.
- **Layer / Content Panel** — visibility, ordering, grouping, and representation controls for map content.
- **Object / Structure Navigator** — semantic browsing of world, regions, settlements, buildings, floors, and other objects.
- **Inspector** — properties and relationships of the selected object or geometry component.
- **Context / AI Assistant Panel** — conversation, explanation, generation, validation, and proposed changes.
- **Timeline / History Controls** — selected historical time, playback, and Branch context, integrated with Core Domain history semantics.
- **Status / Diagnostics Area** — selection state, coordinate readout, validation messages, generation state, and tool hints.

Panels may be rearranged or collapsed as a UI preference, but their responsibilities remain consistent. The shell owns interaction composition, not formal map facts.

### 3.2 Professional editing modes

A mode changes the available tools, inspector sections, defaults, visual emphasis, and task-specific workflows. It does not change the underlying Map identity, Object identity, coordinate authority, history authority, or persistence unit.

| Mode | Primary authoring concerns | Examples of formal content |
|---|---|---|
| Terrain & Natural Geography | Elevation, terrain forms, watersheds, climate/ecology interpretation | Terrain surfaces, mountain ranges, rivers, lakes, ecological regions |
| Settlements & Urban Geography | Settlement hierarchy, districts, land use, road networks, trade routes | Cities, villages, districts, roads, routes, settlement areas |
| Buildings & Interior Spaces | Building structure, floors, rooms, vertical and underground connectivity | Buildings, floors, rooms, caves, corridors, entrances |
| General Map Authoring | Regions, boundaries, labels, POIs, spatial relationships, manual geometry | Regions, boundaries, points of interest, annotations and supported generic spatial objects |

The General Map Authoring mode is the fallback context for objects that do not need a specialist workflow. It must not become a second, unconstrained object model.

### 3.3 Mode switching

Switching modes must preserve:

- selected Map/Branch and Historical Time;
- current viewport and spatial extent unless a mode explicitly requests a contextual zoom;
- selected Object identities where the target mode can represent them;
- unsaved transient editor state that is safe to preserve;
- current visibility and layer state, subject to mode-specific filters.

If a selected object has no dedicated tool in the target mode, it remains selectable and inspectable through the shared object inspector. The UI may indicate that specialized editing is available in another mode; it must not hide or duplicate the object.

## 4. Architectural layers and responsibility boundaries

### 4.1 Editor Shell / Presentation Layer

Responsibilities:
- Avalonia windows, panels, docking/layout preferences, command surfaces, keyboard/mouse interaction, accessibility, and input routing;
- canvas viewport, zoom/pan, selection interaction, hover feedback, snapping feedback, and transient overlays;
- mode activation and tool lifecycle;
- binding UI controls to application-facing read models and service contracts;
- displaying validation, provenance, AI explanations, and history diagnostics.

Must not:
- write Core Domain persistence directly;
- invent or allocate formal Object IDs;
- treat canvas pixels or rendered symbols as authoritative geometry;
- independently calculate Branch inheritance or reconstruct historical state;
- persist editor Undo/Redo entries as formal world history;
- infer that a visual grouping is a semantic parent-child relationship unless explicitly represented in domain data.

### 4.2 Editing Application Layer

Responsibilities:
- coordinate editing sessions and mode-specific use cases;
- translate user gestures into typed editing intents;
- manage transient edit sessions, selection sets, tool state, snapping candidates, and local preview state;
- call geometry/spatial services for operations that do not themselves commit formal domain changes;
- request Core Domain read projections and submit typed authoring operations through Domain Services;
- coordinate Candidate review and AI-assisted workflows with AI Assistant;
- maintain editor-local Undo/Redo for uncommitted interaction operations.

This layer may orchestrate domain services but cannot become a second authority for formal Map/Object/History data.

### 4.3 Shared Spatial & Presentation Model

Responsibilities:
- define shared spatial concepts consumed by all editing modes;
- define coordinate reference context, units, spatial scopes, geometry types, geometry validity, and object-to-geometry associations;
- define shared layer and presentation concepts;
- map semantic Objects to one or more visual representations;
- provide consistent spatial queries and view projections.

The model deliberately distinguishes:
- semantic identity;
- spatial geometry;
- object state at a selected Historical Time;
- layer membership and visual style;
- editor selection and transient interaction state.

These concerns may be represented by related types and services, but must not be collapsed into one mutable UI object.

### 4.4 Core Domain

Core Domain is the sole authority for formal world/map data:
- Project, Map, Branch, History, Calendar and Historical Time;
- stable Object Type and Object identity;
- Object ID reservation and lifecycle;
- formal initial map state;
- formal geometry and semantic attributes/relationships;
- Commands, Events, State Changes, ordering constraints, and historical reconstruction;
- authoritative asset references and checkpoint metadata.

All formal changes must use typed Core Domain Domain Services. The editor consumes authoritative projections for a selected Map/Branch and Historical Time. It must not reconstruct history by querying persistence records independently.

Core Domain's existing distinction between Object identity and ObjectState remains mandatory. Geometry split/merge operations must not implicitly create, destroy, or merge semantic Object identities.

### 4.5 Map Engine Services

Map Engine is a set of local application services, not an independent source of world facts. It provides capabilities such as:
- rendering and viewport projection;
- geometry construction, validation, clipping, buffering, simplification, and spatial predicates;
- coordinate transformations and measurement;
- terrain/elevation processing;
- hydrology and watershed analysis;
- climate and ecological suitability approximations;
- spatial indexing and query acceleration;
- raster/vector asset processing and export rendering.

The Map Engine may cache derived outputs, but cache contents are disposable and cannot replace Core Domain authority. Exact engine libraries and rendering technology remain subject to the separate Map Engine technical evaluation.

### 4.6 AI Assistant

AI Assistant remains the single user-facing assistant. It may:
- interpret story and creator instructions;
- extract World Knowledge entities, assertions, and relations;
- create Spatial Intent proposals through the Story-to-Spatial Bridge;
- plan generation and editing Tasks;
- request geography/geometry operations from deterministic services;
- produce Candidates, validation evidence, explanations, and alternatives;
- prepare Change Sets and request creator authorization according to risk policy;
- submit approved changes through Core Domain Domain Services.

It may not:
- directly write Core Domain tables;
- allocate or recycle Object IDs;
- treat a Candidate as formal map state;
- create an AI-owned parallel map history or World State;
- silently rewrite confirmed creator intent;
- substitute LLM reasoning for deterministic geometry, topology, hydrology, or climate calculations where those calculations are required.

## 5. Shared spatial model

### 5.1 One Map Space, many views

The editor uses one logical Map Space per Map. The Map Space is independent of screen pixels and UI layout. It provides the spatial context in which geometry and spatial relationships are interpreted.

V1 should support a bounded, explicit spatial reference context for each Map, including:
- coordinate model / reference system identity;
- coordinate units and scale interpretation;
- map extent and spatial bounds;
- supported geometry dimensionality and precision policy;
- optional vertical/elevation reference where required by terrain and building workflows.

The exact coordinate reference strategy, projection support, and geometry precision rules are ME-01-04 / ME-05 / Map Engine design decisions. Do not hard-code screen coordinates as world coordinates or assume every fictional world must use Earth longitude/latitude.

### 5.2 Distinct spatial extents

Keep three concepts separate:

1. **Map Extent** — the logical spatial coverage of the Map.
2. **Canvas View** — the current visible viewport and zoom/pan state.
3. **Export Area** — the region selected for rendering or export.

A viewport change must not mutate Map Extent. Export selection must not redefine the Map. Extending a Map's logical coverage is a domain-level authoring operation, not an incidental zoom action.

### 5.3 Multi-scale spatial scopes

Use four working scopes:

- **World** — continents, oceans, global climate drivers, major mountain systems, macro-regions;
- **Region** — regional terrain, watersheds, climate, ecosystems, political/cultural regions;
- **Locality** — cities, villages, roads, land use, local terrain, building groups;
- **Site** — individual buildings, floors, rooms, caves, underground spaces, and facilities.

These are linked scopes and levels of detail, not four independent maps or databases. The same spatial feature may be viewed at different scales with different levels of geometric detail and presentation. Cross-scale identity and containment/association rules are specified in ME-01-04 and ME-05.

### 5.4 Geometry and semantic Object identity

A semantic Object may own or reference one or more geometry components according to its domain type. Geometry is not the Object identity.

Examples:
- a mountain range is one semantic object whose representation may contain multiple ridge/area geometries;
- a river is one semantic object represented by a connected or segmented line geometry and may have derived watershed relationships;
- a city is one semantic object associated with a city area, districts, roads, and many building objects;
- a castle is one stable semantic object with exterior footprint, interior floor plans, and vertical/underground spatial structures.

The geometry model must support geometry refinement, subdivision, generalization, and representation changes without silently changing semantic Object identity. Whether a geometry edit is a formal Command or a baseline authoring operation is governed by Core Domain authoring contracts, not by the rendering engine.

### 5.5 Object, geometry, and editor selection

Keep separate:
- **Object identity** — stable domain identity allocated by Core Domain;
- **ObjectState** — the object's state reconstructed at the selected Historical Time;
- **Geometry** — spatial shape data associated with the object/state;
- **Geometry component / subshape** — editable parts of a geometry representation;
- **Selection** — transient editor state identifying selected objects or geometry components;
- **Representation** — how the object is drawn in a specific view/style/scale.

Selecting a geometry component does not imply selecting a new semantic Object. Editing one component must respect the owning Object's domain invariants.

## 6. Shared layer and presentation architecture

### 6.1 Unified layer model

Use a shared layer system across professional modes with three broad layer kinds:

- **Vector layers** — semantic objects and vector geometry;
- **Raster layers** — elevation grids, raster analysis outputs, image references, and other grid-based data;
- **Group layers** — organizational containers for related layers and visibility control.

Layers are organizational and presentation structures. They do not replace Object identity, semantic relationships, or Core Domain ownership. A layer may contain references to formal objects and derived assets; it must not become the only place where an object's meaning exists.

### 6.2 Semantic content versus visual representation

A formal object and its visual depiction are different concerns. For example:
- a mountain range is a semantic spatial feature;
- a natural-geography view may display its elevation surface, ridge geometry, and relief shading;
- a fantasy-cartography view may display mountain symbols and a stylized ridge pattern.

Both presentations refer to the same underlying spatial facts. A symbol or texture must not become an alternate geometry authority.

### 6.3 Multiple coordinated map presentations

V1 should provide at least two coordinated presentation profiles:

1. **Natural Geography View** — elevation, terrain, mountain systems, rivers, lakes, coastlines, climate, and ecology.
2. **Fantasy Cartography View** — mountain symbols, forest textures, stylized rivers, political boundaries, settlements, fortresses, and labels.

Additional views (political, cultural, historical, ecological, trade/transport) can be composed from the same model later. A presentation profile changes visibility, styling, labeling, and symbolization; it does not fork the spatial data.

### 6.4 Layer visibility and semantic filtering

Mode-specific layer presets may simplify the visible content. Hiding a layer does not delete, deactivate, or change the historical state of its objects. Filtering is a view operation unless the user explicitly invokes a domain operation.

## 7. Editing interaction model

### 7.1 Canvas-centric interaction

The Canvas is the primary spatial interaction surface. The editor should prioritize:
- pan and zoom;
- click selection;
- rectangle selection;
- lasso selection;
- geometry creation and editing;
- snapping and alignment assistance;
- contextual tool activation;
- inspect and edit properties;
- spatial navigation across scales.

Exact gesture bindings and tool inventory belong to ME-01-06.

### 7.2 Shared tools and mode-specific tools

Shared tools include navigation, selection, inspect, measurement, and generic geometry creation. Specialist tools are activated by the current mode, for example terrain sculpting/contour tools, road/settlement tools, or floor-plan/room tools.

A generic geometry tool may create a draft shape. Converting that shape into a semantic object requires an explicit object type and domain-valid creation flow; geometry alone must not silently invent a semantic identity.

### 7.3 Inspector and property ownership

The Inspector should distinguish:
- semantic properties (name, type, classification, creator notes, relationships);
- spatial properties (geometry, location, elevation, dimensions, connectivity);
- historical properties (validity/effective time, status at selected time, history links);
- presentation properties (symbol, label, style, visibility overrides);
- provenance and validation (source, confirmation state, inferred/derived status, diagnostics).

Presentation properties must not be mistaken for formal semantic facts. A label offset is not a location change; a symbol size is not a physical size.

### 7.4 Undo and Redo

Editor Undo/Redo is a local interaction facility for the current uncommitted editing session. It may reverse transient geometry manipulation, selection changes, and staged edits before formal submission.

It is distinct from:
- formal Core Domain Commands and historical State Changes;
- AI Task cancellation/recovery;
- any operation that has already been formally applied.

After formal application, undoing the result must use an explicit supported Core Domain authoring operation or a new compensating change under Core Domain rules. Task cancellation must not implicitly erase a committed map change.

## 8. Authoring lifecycle and AI collaboration

### 8.1 Manual authoring path

1. User selects a Map/Branch and Historical Time.
2. Editor loads the authoritative Core Domain projection.
3. User selects a mode and tool.
4. Tool creates or modifies transient edit state.
5. Application layer validates local geometry and domain preconditions.
6. User reviews the proposed change.
7. Application layer submits a typed operation through Core Domain Domain Services.
8. Core Domain records the formal change and exposes the updated authoritative projection.

The editor may batch several low-level gestures into one meaningful authoring operation where the domain contract permits. UI gestures themselves are not automatically formal Commands.

### 8.2 AI-assisted authoring path

1. User describes a goal or story context in natural language.
2. AI Assistant updates World Knowledge and interprets Spatial Intent through ME-01-02.
3. Task Planning determines required deterministic operations and relevant editing/generation scope.
4. Map Engine / geography services generate or calculate spatial Candidates.
5. Validation checks geometry, topology, spatial constraints, natural geography, and confirmed world knowledge.
6. User reviews preview, alternatives, impacts, and unresolved assumptions.
7. AI Assistant prepares an exact Change Set and requests authorization according to risk policy.
8. Authorized Change Set is applied through Core Domain Domain Services.
9. Core Domain returns formal references and updated projections; AI Assistant records submission/application evidence.

Guided Initialization (ME-01-03) is a product workflow over this path, not a separate task engine or data authority.

### 8.3 Candidate isolation

Generated geometry, object drafts, derived rasters, and proposed relationships remain Candidate/staging data until the existing AI Assistant validation, selection, authorization, and application lifecycle is complete.

A preview may render Candidates alongside formal data with clear visual distinction. It must be impossible to confuse a staged Candidate with an already committed formal Object.

### 8.4 Risk-sensitive confirmation

Use the existing authorization policy. Require explicit creator confirmation for canon-changing, high-impact, ambiguous, destructive, or difficult-to-reverse changes. Do not impose a new blanket approval gate for every harmless low-risk operation. The editor must explain what will change and which confirmed facts or existing objects are affected.

## 9. Historical time and Branch integration

The editor always operates in a selected Map/Branch and Historical Time context. The displayed map is a Core Domain projection for that context, not a mutable global current-state document.

Requirements:
- timeline playback changes the requested Historical Time and refreshes the projection;
- editing a past state follows Core Domain authoring rules and may affect descendants according to Branch inheritance semantics;
- a Branch is not a visual layer and must not be represented as a second editor document;
- Branch selection changes the authoritative history context, not merely the style or layer filter;
- formal changes must obey historical-time and ordering constraints;
- object movement/animation is synchronized with timeline and map rendering, while character/event narration remains a separate storytelling subsystem that can synchronize with the same historical events.

The editor may display time-based animation and playback interpolation as derived presentation. It must not create a second animation history authority.

## 10. Natural geography and specialist services

Terrain and natural geography authoring must use deterministic, explainable specialist services for calculations that require spatial or physical consistency.

Examples:
- elevation and terrain surface operations;
- drainage direction, river networks, lakes, and watersheds;
- latitude/elevation/wind/ocean/relief-informed climate approximations;
- ecological suitability and biome distribution;
- geometry and topology validation.

The LLM interprets creator intent, identifies relevant causes, proposes a plan, and explains results. It does not replace the geography services or directly synthesize authoritative numerical simulation outputs.

V1 should prefer mature libraries and bounded deterministic algorithms. A full atmospheric or hydrological simulator is not a prerequisite. Natural climate uses Earth-based physical principles by default; supernatural/world-specific modifiers are explicit additional inputs and must remain distinguishable from natural-model results.

## 11. Persistence, assets, and cache boundaries

This document does not define physical tables or file formats. It inherits Core Domain P2-09 ownership principles:
- Core Domain owns formal Map/Object/History/geometry/asset-reference metadata;
- AI Assistant owns Task/Candidate/Proposal/Authorization/Submission evidence;
- immutable payload bytes may live in the project-local Asset Store;
- Checkpoints and engine caches are derived and rebuildable;
- one SQLite file does not imply unrestricted cross-module SQL;
- editor preferences, window layout, active tool, and transient viewport state are application settings, not formal map history.

The editor should consume service projections and opaque references rather than querying Core Domain tables directly. Large geometry/raster payloads may be loaded through asset services and cached by the Map Engine.

## 12. Cross-mode consistency rules

The following invariants apply to every professional mode:

1. One Map Space and one formal Core Domain authority per Map.
2. Stable semantic Object identity is independent of geometry and visual representation.
3. Geometry split/merge/generalization does not silently split/merge semantic Object identity.
4. Mode switching never clones or forks formal objects.
5. Layers organize and present content; they do not own semantic truth.
6. Natural-geography and fantasy-cartography views share the same underlying spatial facts.
7. World/Region/Locality/Site are linked scales, not separate map databases.
8. Historical Time and Branch context are supplied by Core Domain.
9. Editor Undo/Redo, formal history, and AI Task recovery remain distinct.
10. Candidates are visually and logically separate from formal map data.
11. LLM cannot bypass validation, authorization policy, or Domain Services.
12. Deterministic geometry/geography calculations remain in specialist services.
13. Local edits must identify bounded downstream impacts and preserve unaffected confirmed content.
14. Character/event narration is not a responsibility of the map renderer; Unit movement animation is a coordinated map/timeline presentation capability.

## 13. Recommended module organization

The following is a logical module proposal, not a requirement for separate processes or assemblies in the first implementation:

```text
WorldView.Desktop
  └─ EditorShell
      ├─ CanvasHost
      ├─ Panels
      ├─ InputRouting
      └─ ModeHost

WorldView.MapEditor.Application
  ├─ EditingSession
  ├─ Selection
  ├─ ToolExecution
  ├─ AuthoringUseCases
  ├─ PreviewAndReview
  └─ CoreDomainAdapters

WorldView.MapEditor.Modes
  ├─ GeneralAuthoring
  ├─ Terrain
  ├─ Settlements
  └─ BuildingsAndInteriors

WorldView.Spatial
  ├─ CoordinateContext
  ├─ GeometryContracts
  ├─ SpatialScopes
  ├─ ObjectGeometryLinks
  └─ SpatialQueries

WorldView.MapPresentation
  ├─ Layers
  ├─ StylesAndSymbols
  ├─ Labels
  └─ ViewProfiles

WorldView.MapEngine
  ├─ Rendering
  ├─ GeometryServices
  ├─ TerrainServices
  ├─ HydrologyClimateServices
  ├─ SpatialIndexing
  └─ AssetProcessing

WorldView.CoreDomain
  └─ Formal map data, object identity, history, commands, reconstruction, assets

WorldView.AIAssistant
  └─ Knowledge, tasks, candidates, validation, authorization, submission evidence
```

Keep these as logical boundaries initially. Split into additional assemblies only when dependency control, test isolation, or build complexity demonstrates a concrete need. Do not introduce process boundaries or service hosting for these modules in V1.

## 14. Open decisions and deferred specifications

ME-01-01 establishes the overall architecture but intentionally does not freeze:
- exact coordinate reference strategy, projections, units, and precision;
- spatial containment/overlap/adjacency/topology semantics across scales;
- exact object-to-geometry cardinality and geometry component persistence;
- geometry command catalog and baseline-edit semantics;
- layer persistence, nested groups, and cross-mode presets;
- exact selection, snapping, tool, and panel interaction specifications;
- 2D/2.5D/3D rendering architecture and concrete libraries;
- terrain raster resolution, tiling, and storage format;
- hydrology/climate algorithm selection and calibration parameters;
- asset lifecycle and export formats beyond Core Domain P2-09 ownership rules.

These are assigned to later ME-01 documents and ME-02..ME-07. Do not infer that an item is decided merely because it appears as a conceptual component here.

## 15. Acceptance criteria

The architecture is acceptable when:

1. A user can open one Map and switch between Terrain, Settlements, and Buildings/Interiors without creating duplicate maps or objects.
2. The same city, mountain range, river, or building can be inspected from relevant modes while retaining stable semantic identity.
3. The editor distinguishes Object identity, ObjectState, geometry, representation, and transient selection.
4. Natural Geography and Fantasy Cartography views can render the same underlying spatial objects differently without data duplication.
5. The user can work from World scale down to Site scale without switching to disconnected databases.
6. The editor loads a selected Map/Branch/Historical Time projection from Core Domain rather than reconstructing history itself.
7. Manual and AI-assisted edits use typed Domain Services for formal application.
8. AI Candidates can be previewed and edited without becoming formal map data prematurely.
9. Editor Undo/Redo cannot silently undo a formally committed historical change.
10. A local edit can identify affected dependencies and preserve unrelated confirmed content.
11. The editor can provide map rendering and geometry/geography calculations through replaceable local services without making the UI the domain authority.
12. The architecture remains implementable as a modular single-user desktop application without distributed infrastructure.

## 16. Consistency review against current project baselines

### 16.1 Core Domain

Consistent:
- Core Domain owns formal Map/Object identity, geometry, relationships, Commands, Events, State Changes, Branch/History, asset references, and reconstruction.
- Object identity is distinct from ObjectState and geometry.
- The editor consumes selected Map/Branch/time projections and does not independently rebuild history.
- The editor does not introduce Object Version, World State Version, Branch Revision Chain, or Command Revision Chain.
- Geometry and UI changes are not allowed to redefine formal domain semantics.

Open integration gates remain those already recorded in Core Domain P2-09: exact physical persistence, Command/Event/State Change contracts, initial-state authoring semantics, object reservation binding, and provider/transaction proof. ME-01-01 does not close them.

### 16.2 AI Assistant Task Planning & Execution Engine

Consistent:
- AI Assistant owns Task/Plan/Step/Tool Call/Operation, Candidate, Validation, Selection, Change Set, Authorization, Submission and Application evidence.
- Formal map application occurs through Core Domain Domain Services.
- Candidates remain isolated from formal state until the established authorization/application path completes.
- The editor is a product client of the AI Assistant and Core Domain, not an alternative orchestration engine.
- Guided Initialization is implemented as a workflow using existing Task Planning, execution, candidate, validation, and application capabilities.

The precise cross-module atomic transaction and application report contract remain subject to the existing AI/Core Domain integration gates.

### 16.3 Map Generation / Map Engine direction

Consistent:
- C# + Avalonia is the desktop direction.
- Rendering, geometry, GIS/spatial algorithms, terrain, hydrology, and climate remain separately evaluated technical concerns.
- The Map Engine is not formal data authority.
- Existing Rust + Bevy code is historical reference, not the target runtime.
- Deterministic services perform spatial calculations; LLM performs interpretation, planning, and explanation.

No concrete rendering or GIS library is frozen by this document.

## 17. Decision summary

Recommended working baseline:
- one unified Map Editor platform;
- specialist editing modes for Terrain, Settlements, and Buildings/Interiors;
- one shared Map Space and shared spatial/object/presentation model;
- stable Object identity separated from ObjectState, geometry, representation, and selection;
- unified vector/raster/group layer system;
- natural-geography and fantasy-cartography views as coordinated presentations of one spatial model;
- World/Region/Locality/Site as linked working scales;
- Core Domain as sole formal map/history authority;
- AI Assistant as the single user-facing assistant and owner of task/candidate/authorization evidence;
- Map Engine as local rendering and deterministic spatial/geography service provider;
- editor Undo/Redo separate from formal history and AI Task recovery;
- no distributed or generic enterprise architecture for V1.

Status: ME-01-01 v0.1 is a recommended architecture working baseline. It is ready to guide the remaining ME-01 specifications, but it is not a physical schema, final UI specification, rendering technology decision, or implementation authorization.