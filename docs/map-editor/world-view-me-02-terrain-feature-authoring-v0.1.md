# World View — ME-02 Terrain Feature Authoring

- Version: 0.1
- Date: 2026-11-21
- Status: **Specialist Feature Workflow Working Baseline** — interaction and validation semantics; exact descriptor schemas and numerical detection methods remain open
- Parent: ME-02 Terrain & Natural Geography Editor V0.1
- Depends on: ME-02 Domain Inventory V0.1; ME-02 Land/Water & Elevation Authoring V0.1; Core Domain P2-09-18/19/20/21/22/24; ME-01-05/06; Map Engine spatial interaction contract
- Scope: Landmass, Mountain Range, Mountain Peak and Terrain Region authoring; feature detection candidates; semantic relationships; terrain consistency checks

## 1. Purpose

Define how creators author and refine the principal semantic terrain Objects supported by the current Core Domain V1 seed set without conflating:
- formal Object identity;
- formal Geometry Component identity;
- continuous elevation/land-water fields;
- detected feature candidates;
- derived classifications and visual representations.

The editor is a specialist mode over the shared Map Space and Canvas. It is not a separate feature database or a generic feature-extraction tool.

## 2. Core decisions

1. Use the existing `landmass`, `mountain_range`, `mountain_peak` and `terrain_region` Object Types for the first terrain-feature workflow.
2. Do not add separate Continent, Island, Forest, Desert, Plain, Plateau, Valley or Ridge Object Types merely to obtain different symbols or tools.
3. Use geometry roles and typed attributes declared by each Core Domain Object Type descriptor; UI controls do not define schema.
4. Detected landforms remain Derived Results or Candidates until explicitly adopted.
5. One semantic Object may span several Working Scopes and have multiple geometry components as permitted by its descriptor.
6. Geometry component add/remove/adjust/refine are bounded operations; split/merge is not enabled as a formal operation by this design.
7. A Mountain Peak belongs to a Mountain Range only through an explicit supported `structural.part_of` relationship; proximity and elevation maxima are not enough.
8. TerrainRegion classification is a bounded semantic category, not an arbitrary user-defined ontology.
9. Elevation, slope, aspect, hillshade and other physical/derived fields remain separate from feature Objects.
10. All formal writes route through the appropriate Core Domain service and Initial State/History/Shared Definition target.

## 3. Landmass authoring

### 3.1 User intents

- “Draw the main continent.”
- “Add several islands as one archipelago.”
- “Make this peninsula narrower.”
- “Keep this coastline, but refine the inland terrain.”
- “This island is independent from the nearby mainland.”

### 3.2 Creation workflow

1. Creator sketches an area or imports a candidate land boundary.
2. Editor shows the temporary outline, map-edge/wrap context and implied land/water mask preview.
3. Assistant may identify disconnected parts and ask whether they are one semantic Landmass or separate landmasses only when identity affects later authoring.
4. Geometry validation checks polygon/ring validity, coverage, seam behavior and map-edge semantics.
5. Creator confirms the semantic identity/grouping and required attributes such as name and bounded landform class.
6. Core Domain allocates Object identity through the reservation lifecycle and creates the formal Object through the correct baseline/authoring service.
7. A derived land/water mask and coastline may be refreshed from the adopted geometry under the declared authority policy.

### 3.3 Multipart and island behavior

- Multipart geometry is permitted only within the Object Type's declared geometry/component cardinality.
- One archipelago may be one Landmass if the creator treats it as one meaningful entity and the descriptor allows it.
- Multiple independent islands may be separate Landmass Objects.
- Connected-component detection can suggest grouping but cannot decide Object identity.
- A coastline is derived from land/water boundaries; it is not an automatically created separate Object.
- A peninsula or island is not a new type unless the type descriptor explicitly requires a bounded semantic classification.

### 3.4 Validation

- valid polygon/multipart geometry and ring rules;
- coordinate reference, precision and wrap/seam validity;
- no accidental geometry clipping at Canvas boundaries;
- consistency with land/water mask, with source authority disclosed;
- no duplicate or overlapping formal identity created by import/detection without review;
- formal geometry component role/cardinality follows descriptor.

## 4. Mountain Range authoring

### 4.1 Semantic model

A Mountain Range is a meaningful mountain system, not merely a line symbol. Its descriptor may support:
- `extent`: one or more Area components;
- `ridge`: one or more Line/Curve components;
- at least one supported geometry representation overall.

A range may cross Regions/Working Scopes and must retain one semantic identity. The editor must not duplicate it per viewport or per terrain tile.

### 4.2 Creation workflows

**Sketch-first**
1. Creator draws a ridge line or approximate range area.
2. Editor previews geometry and labels it approximate if elevation support is absent.
3. Creator names/classifies the range where required.
4. The system may offer an elevation-shaping preview using the range as a constraint, but does not silently create or modify the elevation field.
5. Creator adopts the semantic Object and, separately, any field proposal.

**Elevation-first**
1. Elevation analysis detects a candidate ridge/mountain system.
2. The result is shown as a candidate overlay with scale, threshold and method.
3. Assistant explains the evidence and limitations.
4. Creator may create a Mountain Range Object from the candidate or ignore/refine it.
5. Formal geometry and field source lineage remain separate.

### 4.3 Geometry and feature refinement

- Adjust an existing component through the typed geometry edit contract.
- Add a new component only when the Object Type role/cardinality allows it and the Core Domain add-component descriptor is enabled.
- Remove a component only if the Object remains valid after removal and Core Domain confirms the operation.
- Refine approximate geometry while retaining source fingerprint and provenance.
- Do not split a range into multiple Objects merely because the ridge is discontinuous or crosses a regional boundary.
- Do not merge separate ranges because their lines touch or overlap.

### 4.4 Mountain/terrain checks

The editor may derive and display:
- ridge continuity and orientation;
- elevation support/consistency;
- relation to nearby peaks;
- possible drainage divide;
- windward/leeward exposure context;
- gaps or discontinuities in the source elevation.

These are diagnostic/derived relationships. They do not automatically create semantic part-of, adjacency, flow or other formal relationships.

## 5. Mountain Peak authoring

### 5.1 Semantic model

A Mountain Peak is an independently meaningful summit, represented by exactly one Point location in the current seed descriptor. Local maxima in an elevation raster are not automatically formal Peaks.

Elevation is a typed value requiring:
- numeric value;
- explicit length unit;
- vertical reference identity;
- source/provenance or authoring method.

Renderer Z, screen height and arbitrary visual prominence are not formal elevation.

### 5.2 Creation and refinement

- Place a Peak directly on the Canvas or adopt a detected candidate.
- If the elevation field is valid at that location, the Inspector may display sampled elevation as a derived reference value.
- The creator may confirm that value as the formal typed Peak elevation, or enter a supported value with unit/reference.
- If no valid elevation source exists, Peak location may still be authored while elevation remains absent/unknown if the descriptor permits.
- Moving the point updates the preview and revalidates sampled elevation; it must not silently rewrite a previously confirmed formal elevation value.
- Candidate peaks are not assigned formal IDs until adopted.

### 5.3 Relationship to Mountain Range

The supported initial `structural.part_of` endpoint pair MountainPeak→MountainRange may be used when explicitly accepted and validated.

Validation should consider:
- endpoint Object types and lifecycle/presence in the selected Map/History;
- declared relationship cardinality and acyclic policy;
- geometric/elevation evidence as diagnostic support;
- whether the creator explicitly intends the peak to be part of the range.

Being close to a ridge is not sufficient to establish the relationship. A peak can be geographically near a range without semantically belonging to it.

## 6. Terrain Region authoring

### 6.1 Purpose and classification

TerrainRegion represents a bounded area with a registered terrain/land-cover classification. The current field catalog expects:
- `extent`: one or more Area components;
- optional canonical name;
- required bounded `terrain_class`.

Forest, desert, plain, wetland, tundra and similar categories may be represented through a bounded TerrainRegion classification where descriptor semantics allow. They are not separate Object Types in the initial seed set.

Climate classification, biome suitability, slope, soil and other analytical layers are not automatically TerrainRegion attributes. They remain derived fields unless a later descriptor explicitly adopts a particular semantic fact.

### 6.2 Authoring paths

- **Creator-drawn region:** draw an area and choose from registered classes.
- **AI-proposed region:** assistant interprets prose or constraints and proposes a classification/geometry candidate.
- **Analysis-derived candidate:** classification from elevation/climate/soil/water inputs; review its method, resolution and uncertainty.
- **Imported region:** stage external polygons and map their categories to the registered enum; unmapped values require an explicit mapping decision.

### 6.3 Validation and overlap

Terrain regions may overlap when their semantics allow it (for example, different classification dimensions), but the same single classification layer should not contain accidental ambiguous overlapping regions unless its descriptor defines precedence or multi-membership.

V1 must not invent a universal layer ontology to solve overlap. The specialist descriptor should state:
- whether the class set is mutually exclusive within one classification scheme;
- whether multiple regions can overlap;
- whether gaps are allowed;
- whether adjacency is formal, derived or irrelevant;
- how classification changes are authored and validated.

A derived biome suitability overlay must not be mistaken for a formal creator-authored TerrainRegion.

## 7. Derived feature detection

Feature detection may suggest:
- landmass connected components;
- mountain/ridge candidates;
- local peak candidates;
- plateau/basin/valley candidates;
- terrain-region classification candidates.

Every detection result must record:
- source field/geometry fingerprints;
- detection method/version;
- parameters and thresholds;
- target scale/resolution;
- coverage;
- confidence/limitations;
- status (candidate, derived, stale or rejected).

Detection rules:
- no formal Object ID is allocated during detection;
- no Object is created because a pixel/vertex crosses a threshold;
- detection candidates are not automatically merged with existing formal Objects;
- creator can accept, reject, edit or defer candidates;
- if candidate conflicts with confirmed creator intent, preserve the intent and explain the conflict.

## 8. Shared selection, Inspector and Canvas behavior

- Selecting a formal feature highlights its formal geometry and shows its stable identity/type.
- Selecting one geometry component shows role, component identity and source Asset/provenance; it does not change Object selection identity.
- Selecting a derived candidate highlights it using a distinct overlay and shows method/parameters rather than a formal Object ID.
- Selecting a field sample reports value/unit/reference, resolution and no-data state; it does not create a semantic Object selection.
- Multiple selections support batch inspection and safe operations only when the relevant domain service explicitly supports them.
- Canvas hit testing consumes the active immutable scene generation; stale hits are discarded.
- The right Inspector separates semantic attributes, geometry components, relationships, physical-field references and derived diagnostics.
- The bottom AI conversation can act on current selection/scope but must show the interpreted target before a high-impact operation.

## 9. Formal authoring target and history

Before formal application, determine whether the change is:
- Map Initial State baseline authoring;
- post-zero Map/Branch historical Command;
- Project Shared Object current-definition authoring.

For typical world geography creation, the initial terrain baseline is normally authored through the Map Initial State path. A later event such as a historically occurring volcanic eruption or flood may require an Event and/or supported History-local operations, but those domain operations are not introduced by this feature editor.

Do not turn a present-day geometry edit into a historical event simply because the user is viewing a historical time. The user must explicitly express a historical change and the relevant Core Domain descriptor must exist.

Presentation-only symbol/label/style changes do not modify formal terrain facts. Candidate review does not itself allocate Object IDs or commit formal data.

## 10. Failure and recovery

- Invalid geometry remains in preview and is not formally applied.
- A detected feature result becoming stale after elevation changes must be recomputed or clearly marked stale.
- If an Object is modified between preview and application, revalidate exact source geometry and target state.
- Unsupported relationship or operation is reported as unavailable; do not emulate it through generic payload writes.
- Failed Object creation must not leave a dangling reservation or partial geometry reference; use Core Domain's reservation/transaction lifecycle.
- Cancelling a feature detection task does not delete previously formal Objects.
- Rejecting a candidate does not remove its source field or formal features.

## 11. Acceptance criteria

1. Landmass identity is creator/domain-defined, not inferred solely from raster connectivity.
2. One MountainRange identity can span multiple Working Scopes without duplication.
3. MountainPeak creation does not occur automatically for every raster local maximum.
4. Peak elevation always has explicit units and vertical reference when formally stored.
5. MountainPeak→MountainRange part-of relation requires explicit supported semantics and validation.
6. TerrainRegion uses bounded registered classification; no arbitrary runtime Object Type or enum creation.
7. Derived biome/climate/slope results remain distinct from formal TerrainRegion Objects.
8. Feature detection preserves source fingerprints, method/version, parameters, scale and uncertainty.
9. Geometry component edits follow Core Domain descriptors and preserve semantic Object identity.
10. Split/merge is not exposed as a formal V1 operation without adopted descriptors.
11. UI clearly distinguishes formal Object, geometry component, field sample, derived result and Candidate.
12. All formal application uses the correct Initial State/History/Shared Definition path.

## 12. Open gates

| ID | Gate | Owner |
|---|---|---|
| TF-01 | Final `mountain_range` role cardinality and add/remove component availability | Core Domain |
| TF-02 | Final `landmass` multipart/semantic grouping rules | Core Domain + ME-02 |
| TF-03 | Peak elevation unit/vertical reference catalog and sampling rules | Core Domain + Spatial Services |
| TF-04 | TerrainRegion initial enum and overlap/gap semantics | Core Domain + ME-02 |
| TF-05 | Feature detection algorithms, thresholds and scale calibration | Natural Geography Services |
| TF-06 | Candidate-to-Object adoption and duplicate detection workflow | AI Assistant + Core Domain |
| TF-07 | Exact Map Initial State authoring API and transaction behavior | Core Domain |
| TF-08 | Derived candidate freshness and scene overlay contract | Map Engine + Editor |

## 13. Status

This document establishes specialist creator workflows and validation boundaries for the first formal terrain feature types. It does not freeze new Object Types, attribute schemas, feature detection algorithms, relationship cardinality or implementation libraries.

Hydrology is the next dependent specialist workflow. It must consume the land/water, elevation, map-edge and terrain feature contracts already established and must align formal flow relations with P2-09-22.
