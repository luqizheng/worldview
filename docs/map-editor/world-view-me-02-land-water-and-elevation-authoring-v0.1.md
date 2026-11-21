# World View — ME-02 Land/Water & Elevation Authoring

- Version: 0.1
- Date: 2026-11-21
- Status: **Specialist Authoring Workflow Working Baseline** — user workflow and logical data contract; physical field schema, tiling, numerical algorithms and persistence services remain open
- Parent: ME-02 Terrain & Natural Geography Editor V0.1
- Depends on: ME-01-04/05/06; ME-02 Domain Inventory V0.1; Natural Geography, Climate & Hydrology Model V1.0; Map Rendering V0.3; Spatial Presentation & Editing Interaction Contract V0.1; Core Domain P2-09-19/25
- Scope: World frame/map-edge semantics, land/water authoring, elevation field authoring, field inspection, preview, validation and adoption boundaries

## 1. Purpose and decisions

Land/water structure and elevation are the foundational inputs for most natural-geography analysis. This document defines how a creator authors them without requiring GIS expertise and establishes a logical boundary between:
- semantic Landmass Objects and their formal geometry;
- continuous land/water and elevation fields;
- temporary sketch/brush previews;
- derived coastline, terrain and analytical outputs;
- formal Core Domain application.

### 1.1 Recommended decisions

1. The creator authors a world through familiar spatial language and direct Canvas tools; CRS, axis order and numeric precision remain system-managed unless advanced details are requested.
2. Land/water is a foundational categorical spatial field and may also be constrained by formal Landmass geometry. Neither representation silently replaces the other.
3. Elevation is a continuous physical field, not one Object per cell.
4. A semantic Landmass has stable identity independent of connected raster components, viewport clipping or LOD.
5. Coastline is derived from the land/water boundary and map-edge semantics; Canvas/map frame edges are not automatically coastlines.
6. Field edits are performed in transient Edit Sessions and published as immutable candidate/source Assets through a future typed field-authoring boundary. Brush samples are never formal Commands.
7. Imported source data, creator-authored data and algorithm-derived data remain distinguishable.
8. Field coverage, resolution, units, spatial reference, no-data semantics, source fingerprint and derivation provenance are mandatory logical metadata.
9. Local edits preserve parent/neighbor boundary context and invalidate only dependent analyses.
10. This document does not decide the final raster format, tile layout, field database tables, exact brush algorithm or geometry library.

## 2. Creator-facing world frame setup

### 2.1 Ask creative questions, not GIS questions

When world-frame information is missing and materially affects the result, the assistant should ask in natural language, for example:
- Is this a whole planet-like world, a continent, an island group or a local region?
- Should the map wrap around at the left/right edges like a globe?
- Are the top and bottom edges actual world boundaries, or are they just the edge of the current map?
- Is north fixed at the top, or does the world use a different orientation?
- Is the world broadly Earth-like, or are its size, gravity, axial tilt or seasons intentionally different?

Do not ask the user to provide EPSG identifiers, CRS WKT, axis order, raster cell size or numeric precision unless they explicitly enter an advanced technical workflow.

### 2.2 World frame logical definition

The world frame must logically identify:
- spatial reference identity and reference kind (Geographic, Projected or LocalCartesian);
- coordinate unit, axis order and dimension;
- origin/basis/transform or datum/projection parameters as applicable;
- map scale/world size convention;
- valid coordinate domain and wrap/seam policy;
- optional vertical reference for elevation;
- map-edge semantics and declared coverage.

Use the immutable spatial-reference definition rules from P2-09-25. Do not require EPSG. A spatial reference change is an explicit conversion that publishes new geometry/field Assets; it must not silently reinterpret existing coordinates.

### 2.3 Map-edge semantics

Every map edge or coverage boundary must be one of:
- **Closed world boundary:** the authored world actually ends here.
- **Wrap/seam:** opposite edges connect under the declared world frame.
- **Partial coverage:** the map/data stops here but the world may continue.
- **Unknown boundary:** insufficient information to determine continuation.

The edge state is not inferred from the Canvas crop. A coastline is derived only where valid land/water data transitions between land and water, not where data ends.

For a wrap/seam:
- calculations must use wrap-aware neighborhood and connectivity rules;
- coastlines and elevation interpolation must not create artificial discontinuities at the seam;
- cross-seam geometry and field dependencies must be validated;
- export clipping must not change formal world topology.

## 3. Land/water authoring

### 3.1 Logical content

Land/water authoring may combine:
- semantic Landmass Objects and their `extent` geometry;
- a categorical land/water mask;
- inland water geometry such as Lake Objects;
- unknown/outside coverage.

The mask should at minimum distinguish:
- land;
- ocean/open water;
- inland water;
- unknown/no-data;
- outside formal coverage.

Additional coastal/wetland/ice categories must be introduced only when their semantics and model use are defined. Do not overload one class to mean both physical water and a cartographic style.

### 3.2 Relationship between Landmass geometry and mask

The two representations have different responsibilities:

- Landmass geometry expresses creator-recognized semantic land entities and stable identity.
- Land/water mask expresses the spatial classification required for continuous physical analysis.
- Derived coastline expresses boundaries in a chosen resolution/representation.

The system may rasterize formal Landmass geometry to initialize a mask, or derive Landmass candidates from an imported/creator-authored mask. Such derivation does not automatically create or split formal Landmass Objects.

When they disagree:
1. detect the disagreement;
2. identify the source and authority of each representation;
3. show the affected region;
4. determine whether one is a stale derived output or whether the creator supplied two conflicting authored inputs;
5. request a decision only when the conflict cannot be resolved by declared authority/provenance rules.

Do not silently overwrite formal geometry with a mask or silently rewrite a creator-authored mask to match a vector boundary.

### 3.3 Land/water authoring tools

Initial tool behaviors:
- **Sketch landmass:** draw an approximate Area geometry and preview the implied mask/coastline.
- **Edit land boundary:** adjust vertices/curves through the shared geometry interaction contract.
- **Paint land/water mask:** paint a bounded categorical region in a transient field Edit Session.
- **Fill/replace region:** apply a deterministic region operation after previewing affected area and connectivity.
- **Import land/water data:** stage external data, inspect CRS/coverage/class mapping and preview resampling.
- **Inspect boundary:** identify coastline, lake shoreline, map edge, unknown coverage and seam behavior separately.

Do not present vector geometry and mask painting as interchangeable operations. The Inspector should state whether the user is editing a semantic Landmass geometry, a physical mask, or both through an explicitly coordinated operation.

### 3.4 Islands and identity

- A connected land component is a geometric/analytical result, not an automatic Object identity.
- A single Landmass may have multipart geometry when the creator intends one semantic archipelago or grouped land entity and the descriptor permits it.
- Several nearby islands may instead be distinct Landmass Objects.
- The creator's semantic intent determines identity; connectivity algorithms can suggest groupings but cannot decide them.
- Splitting geometry does not split Landmass identity. Geometry split/merge formal operations remain subject to the Core Domain catalog and must not be emulated by untracked Asset replacement.

## 4. Elevation field authoring

### 4.1 Logical definition

Elevation is a continuous scalar field over a declared spatial coverage.

It must identify:
- spatial reference;
- coordinate and elevation units;
- vertical reference/datum where applicable;
- horizontal coverage and data resolution;
- value precision/quantization profile;
- no-data/unknown semantics;
- source/provenance;
- derivation method/version and parameters if derived;
- source-state fingerprint and freshness.

A numeric elevation without a unit and vertical reference is not a complete formal elevation value. Mountain Peak's typed elevation attribute must use the same explicit unit/reference semantics. Renderer Z and 3D display height are not formal elevation.

### 4.2 Authoring modes

Support a small set of understandable authoring methods:

1. **Broad elevation shaping:** creator sketches broad highland/lowland regions and the system interpolates a continuous field.
2. **Terrain brush:** raise, lower, smooth or blend a local region with previewed strength and footprint.
3. **Profile control:** creator draws a line and edits a cross-section/profile at selected control points.
4. **Feature-guided shaping:** use a Mountain Range/Ridge candidate, plateau or basin intent as a constraint to propose a field shape.
5. **Imported elevation:** load a supported raster/grid source, inspect its reference, units, coverage, resolution and no-data before adoption.
6. **Derived refinement:** refine a selected region using deterministic interpolation/resampling while preserving boundary conditions and source lineage.

These methods are authoring workflows over a field, not separate formal Object Types.

### 4.3 Terrain brush interaction

A brush operation must expose creator-understandable controls:
- action: raise / lower / smooth / blend;
- affected region and brush footprint;
- strength or target elevation range where meaningful;
- falloff/transition style;
- whether nearby boundary conditions are preserved;
- preview and affected-area overlay.

The application may expose numeric advanced parameters in an Inspector, but normal creation should not require them.

Brush behavior:
1. Begin an Edit Session against a pinned source field fingerprint.
2. Convert pointer positions through the Map Engine typed coordinate pipeline.
3. Build transient preview output; do not write formal Assets per pointer sample.
4. Show boundary/neighbor impact and dependent analysis invalidation.
5. Allow cancel, undo within the transient session or revise parameters.
6. On explicit completion, produce a candidate immutable field Asset and validation report.
7. Adopt through the future field-authoring service only after the target authoring semantics and persistence contract are available.

No generic `core.geometry.adjust_component` Command is assumed to cover raster field painting. The vector geometry catalog does not define a raster patch Command.

### 4.4 Feature-guided elevation

A creator may sketch a mountain range or basin before elevation exists. The system may:
- use the feature geometry as a constraint/guide;
- propose broad elevation gradients;
- preserve creator-defined ridge/extent location;
- explain how the proposal affects drainage, wind exposure and local climate;
- keep the elevation proposal separate from the formal Mountain Range Object.

A Mountain Range Object does not automatically imply a specific elevation profile. A peak marker does not imply an exact surrounding terrain surface.

### 4.5 Resolution and refinement

- Resolution describes the spatial detail of source data, not the current zoom level.
- World-scale and regional fields may use different resolutions/representations while remaining part of one continuous spatial model.
- Regional refinement uses parent/neighbor context and preserves declared boundary conditions.
- Do not invent local precision from coarse source data.
- Resampling/interpolation method and output resolution must be explicit and provenance-tracked.
- LOD and renderer simplification never overwrite authoritative field data.

## 5. Logical field data contract

The following is a logical contract to guide ME-05/ME-07; it is not a physical schema.

### 5.1 Field Dataset

A Field Dataset identifies a spatially coherent physical field family and its coverage/resolution organization. It must declare:
- stable identity;
- field family and semantic description;
- spatial reference identity;
- coverage/extent and map-edge behavior;
- value type, unit and vertical reference if applicable;
- resolution profile(s);
- no-data and unknown semantics;
- canonical source/authoring provenance;
- immutable content/asset references;
- source and derived dependency references;
- compatibility/version metadata.

Whether Field Dataset is a Core Domain entity, a typed Asset owner or an application-level aggregate remains open. It must not become a competing map authority.

### 5.2 Field Revision / publication

A field update should be immutable and reproducible:
- each published content state points to immutable Asset bytes;
- source Assets remain retained while referenced by formal history or derived dependencies;
- derived output records the exact source fingerprints and method/version;
- replacing an authored field publishes new content rather than mutating bytes;
- failed publication leaves the previous valid field untouched.

The term “Field Revision” here is logical content lineage, not a decision to add a generic revision-chain table. Exact lifecycle/persistence must be decided by Core Domain and Asset contracts.

### 5.3 Spatial sampling and tile semantics

The field contract must define:
- sample location convention (cell center, node, or other explicit convention);
- grid origin, axis orientation and spacing;
- tile bounds and overlap/halo policy;
- interpolation/resampling method;
- no-data propagation;
- boundary and wrap behavior;
- quantization and deterministic serialization;
- source fingerprint and algorithm version.

Adjacent tiles must agree on shared boundaries or use an explicit halo/overlap strategy. Tile seams must not create artificial elevation cliffs or drainage discontinuities.

### 5.4 Unknown versus zero

No-data is not zero. Unknown/outside coverage must remain distinct from:
- elevation zero;
- sea level;
- ocean mask;
- lake water;
- empty/transparent presentation.

Calculations must propagate unknown inputs or explicitly state the imputation rule. The system must never silently fill missing elevation with zero and then claim a valid drainage/climate result.

## 6. Derived outputs and dependent analysis

Land/water and elevation changes can invalidate:
- coastline and island-connectivity candidates;
- slope/aspect/hillshade/contours;
- drainage divides, sinks, flow direction, accumulation and watershed polygons;
- river channel candidates and river-flow diagnostics;
- temperature lapse and wind exposure;
- orographic precipitation/rain shadows;
- climate classifications and biome suitability.

The system should present a compact impact summary:
- directly affected area;
- dependent result families to refresh;
- neighboring context required;
- protected formal Objects/geometry that will not be changed;
- outputs that will become stale until recomputed.

Do not recompute unrelated world areas merely because the user changed one local field patch.

### 6.1 Land/water change impact

A land/water change may affect:
- coastline and coastal topology;
- lake/ocean distinctions;
- maritime climate influence and moisture sources;
- river outlets and basin drainage;
- coastal ecosystems and settlement suitability.

The impact boundary must include relevant hydrological/coastal dependencies, not just the edited pixels.

### 6.2 Elevation change impact

An elevation change may affect:
- local slope/aspect;
- ridge/valley and drainage divides;
- sink/depression interpretation;
- river flow direction/accumulation and watershed boundaries;
- temperature lapse;
- wind exposure, orographic uplift and rain shadow;
- climate and biome suitability.

A change to cartographic hillshade settings alone affects none of these physical outputs.

## 7. Validation rules

### 7.1 World frame and field validation

- spatial reference exists and matches the Asset envelope;
- axis order, units and dimension are valid;
- coordinate/value numbers are finite and within declared domain;
- vertical reference exists for formal elevation values;
- coverage and resolution are declared;
- no-data semantics are explicit;
- wrap/seam and map-edge rules are compatible;
- deterministic serialization/hash is reproducible;
- source provenance and license/attribution are retained for imports where applicable.

### 7.2 Land/water validation

- categorical values are registered and valid;
- land/water mask coverage does not silently treat unknown as ocean or land;
- mask/vector disagreement is diagnosed;
- map edge is not mistaken for coastline;
- wrap seam does not create false coastlines or disconnected land;
- raster connected components do not automatically create formal Landmass Objects;
- Lake shoreline and land/water mask semantics are consistent or explicitly unresolved.

### 7.3 Elevation validation

- values have valid units and vertical reference;
- no-data is not treated as zero;
- unsupported cliffs/gradients and tile seams are flagged;
- precision is appropriate to extent and minimum meaningful feature size;
- derived elevation does not claim more detail than its inputs;
- boundary conditions are preserved where required;
- field publication does not overwrite a newer source fingerprint.

### 7.4 Severity and exceptions

- **Blocking:** invalid reference/units, malformed Asset, unsupported operation, invalid source fingerprint, or unsafe formal application.
- **Warning:** suspicious gradient, seam artifact, incomplete coverage, uncertain interpolation, unusual but possible terrain.
- **Information:** resolution, provenance, method/version, assumptions, coverage and expected limitations.

Explicit creator-authored magic, terraforming or engineered intervention may explain a physical discontinuity. Preserve the exception as a sourced constraint/override; do not erase it as an error correction.

## 8. Import and external source workflow

Import must be a controlled staging workflow:

1. inspect file type and supported format;
2. read embedded spatial reference, axis, units, vertical reference, coverage and resolution;
3. if metadata is missing/ambiguous, ask the creator or mark unresolved; do not guess;
4. show preview of extent, orientation, no-data, value range and transformation required;
5. allow explicit clipping/resampling/reprojection preview;
6. preserve original source Asset and provenance;
7. validate transformed output and compare source/output coverage and precision;
8. stage as candidate/source data;
9. adopt only through the approved field/geometry ownership path.

Initial import formats remain a ME-07 technology decision. GeoTIFF/COG/ASCII Grid/GeoJSON may be evaluated as candidates, but this document does not claim any format is currently implemented or selected. GeoJSON does not represent arbitrary elevation rasters.

## 9. UI states and user feedback

For field editing, the user should be able to distinguish:
- **Source / authored field:** creator-authored or imported source.
- **Derived field:** computed output with method/version and source links.
- **Preview:** transient uncommitted edit.
- **Candidate:** task-staged output awaiting validation/review/application.
- **Stale:** source dependencies changed; output is no longer current.
- **Unavailable / unknown:** no supported data for the requested area.

The Inspector should show a compact summary by default and expose technical metadata on demand:
- field name/type;
- area coverage and resolution;
- units/reference;
- source or method;
- freshness and validation status;
- dependent results;
- edit/preview state.

The Canvas should use visually distinct overlays for formal Landmass boundaries, land/water mask, elevation preview, derived coastline and unknown coverage. Do not use the same visual style for formal geometry and transient brush output.

## 10. Failure and recovery

- Cancelled brush/import/calculation sessions must not alter the previous valid field.
- A failed tile write or Asset publication must not leave a partially updated field presented as complete.
- A stale preview must be discarded or explicitly rebased; never silently apply it over newer source data.
- Partial coverage must be visible and labelled partial.
- When a region is missing elevation, downstream hydrology/climate outputs must lower confidence or remain unavailable instead of inventing values.
- If a calculation fails for one tile/region, preserve valid unrelated areas and report exact affected coverage.
- AI/network failure must not block manual editing of already available data.

## 11. Acceptance criteria

1. A creator can set up a usable world frame without entering GIS identifiers.
2. Map-edge semantics distinguish true boundary, wrap/seam, partial coverage and unknown.
3. Canvas edges never automatically become coastlines.
4. Landmass semantic identity is independent of raster connected components.
5. Land/water mask and Landmass geometry have explicit authority/provenance and conflict handling.
6. Elevation is a continuous field with units, vertical reference, coverage, resolution and no-data semantics.
7. No-data is never silently treated as zero or sea level.
8. Brush and import workflows use transient preview and immutable publication; no per-pointer formal writes.
9. Derived coastline/slope/hillshade/contours remain derived and reproducible.
10. Local edits preserve neighbor/parent context and invalidate only declared dependants.
11. Stale preview cannot overwrite newer field content.
12. Imported source retains original data, CRS/units, attribution and transformation provenance.
13. Manual authoring remains possible without AI/network services.
14. Exact field storage, tile layout and raster library are not falsely described as frozen before ME-05/ME-07 validation.

## 12. Open gates

| ID | Gate | Owner | Required closure |
|---|---|---|---|
| LW-01 | Field Dataset owner and persistence lifecycle | Core Domain + ME-05 | Typed ownership/CRUD-authoring and history semantics |
| LW-02 | Field-to-Initial-State relationship | Core Domain | Define time-zero field baseline and later historical field change behavior |
| LW-03 | Raster/field Asset envelope and tile layout | Map Engine + persistence | Immutable asset/tile metadata, canonical serialization and integrity |
| LW-04 | Sample location, grid origin, halo and seam rules | Spatial Services | Deterministic grid contract |
| LW-05 | Elevation vertical-reference catalog and conversion | Core Domain + Spatial Services | Typed units/reference validation and conversion fixtures |
| LW-06 | Land/water vector-mask reconciliation | ME-02 + Core Domain | Authority/provenance and conflict resolution rules |
| LW-07 | Brush operations and interpolation algorithms | Natural Geography Services | Deterministic algorithm/version/parameter contract |
| LW-08 | Import format and transform library selection | ME-07 | Windows/macOS technical spike and round-trip tests |
| LW-09 | Derived-field cache retention/freshness | Map Engine | Fingerprint/invalidation and rebuild contract |
| LW-10 | Numeric precision profile per spatial reference | Spatial Services | Precision test corpus and profile generation rules |

## 13. Status

This document establishes the creator-facing workflow and logical field contract for land/water and elevation authoring. It does not define executable schema, final field Asset format, tile storage, selected raster library, exact interpolation algorithm or production API.

The next ME-02 work should define terrain feature authoring and validation while the field ownership/persistence gates are carried into ME-05/ME-07. Hydrology design must consume the explicit land/water, elevation, no-data, seam and coverage contracts established here.
