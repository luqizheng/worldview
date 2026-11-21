# World View — ME-05-01 Field Dataset Authority & Persistence Contract

- Version: 0.5
- Date: 2026-11-21
- Status: **Adopted Domain Contract — V1 product semantics confirmed; design baseline, not a frozen ADR or executable schema**
- Owner: Core Domain (formal Field Dataset identity, scope, revision and Asset references); Map Engine / Spatial Services (field decoding, sampling, calculation and derived outputs)
- Closes: ME-02-H H-G01 / Source Field Dataset authority gate at the domain/product-semantic level; implementation and provider-proof gates remain open
- Depends on: ME-01-04/05/07; ME-02-B Land/Water & Elevation Authoring; ME-02-F Calculation Contracts V0.4; ME-02-H Cross-architecture Review V0.2; P2-09-11 Physical Schema V0.11; P2-09-12 Provider / Transaction Proof V0.6; P2-09-16 Shared Definition / Checkpoint Coherence; P2-09-25 Geometry Asset Encoding and Coordinate Precision; P2-09-30 Field Descriptor Registry V0.1; P2-09-31 Field Dataset Validation Vectors V0.1; ME-05-05 Geometry & Field Asset Package/Publication V0.2; ME-05-07 Field Payload Package & Tiling V0.2; ME-05-08 Field Package Binary Envelope V0.1; P2-09-33 Binary Envelope Validation V0.1; P2-09-32 Field Package/Tiling Validation V0.1

## 1. Purpose and recommendation

ME-02 defines physical fields such as elevation and land/water masks as first-class source inputs, but intentionally leaves their formal owner and persistence lifecycle open. ME-02-H records this as the only P0 cross-architecture implementation gate.

This proposal closes that gap with a small, explicit model:

1. A **Field Dataset** is a stable, Map-owned semantic source field identity, such as elevation or land/water.
2. A **Field Revision** is an immutable published content state of that dataset.
3. Each published Field Revision references immutable Core Asset content through a typed Field Revision → Asset ownership link.
4. Core Domain owns Field Dataset identity, Map scope, schema/semantics compatibility, revision identity, current-revision pointer and formal provenance.
5. Map Engine / Spatial Services reads the current source revision and owns deterministic sampling/calculation; it owns Derived Artifacts, not source Field Dataset authority.
6. The Editor and AI Assistant may stage, preview and propose field changes, but cannot directly write Core Domain or mutate published Asset bytes.

**V1 scope recommendation:** source Field Datasets belong to a Map-level Source Field Baseline. They are not Project-shared definitions, not Branch-local overlays, not Map Initial Object State rows and not post-zero History Commands. A new published revision becomes the current source baseline for every Branch and every Historical Time of that Map. This is a source-data correction/update, not an in-world historical event.

This is the adopted V1 model. It preserves Map isolation, immutable source provenance, deterministic calculation inputs and the existing Core Domain History model. The Map-wide retroactive effect was explicitly confirmed by the product owner on 2026-11-21.

## 2. Scope and non-goals

### In scope

- Stable Field Dataset identity and Map ownership.
- Immutable Field Revision identity and provenance.
- Typed immutable Core Asset ownership.
- Field schema, spatial reference, coverage, resolution, units, vertical reference and no-data semantics.
- Current revision publication and compare-and-swap behavior.
- Map/Branch/History interaction and Checkpoint / calculation invalidation.
- Source-field authoring/import staging and formal adoption boundary.
- V1 built-in field keys and validation requirements.

### Out of scope

- Choosing GeoTIFF, Cloud Optimized GeoTIFF, Zarr, custom binary, compression or exact package format.
- Choosing raster/GIS/geometry libraries or GPU compute.
- Defining every possible physical field (soil, climate, wind, vegetation, water depth, etc.).
- Time-varying geological simulation or general raster history.
- Branch-specific field overrides or field merge/rebase.
- Derived Artifact persistence and calculation scheduling, which remain ME-02-F / Map Engine responsibilities.
- Renderer SceneSnapshot, spatial indexes, GPU buffers and viewport caches.
- Generic Asset CRUD, user-defined executable field schemas or a general-purpose raster algebra engine.

## 3. Authority boundaries

| Concern | Owner | Contract |
|---|---|---|
| Field Dataset identity and Map ownership | Core Domain | One stable identity per Map + registered field key |
| Field schema and semantics compatibility | Core Domain static Field Descriptor Registry | Typed field kind, value semantics, units and compatibility |
| Published Field Revision metadata | Core Domain | Immutable revision record and current pointer |
| Source field bytes | Core Asset Store | Immutable, content-hashed Asset |
| Field decoding, sampling and spatial operations | Map Engine / Spatial Services | Read-only access to the published revision; deterministic interpretation |
| Derived calculations and outputs | Map Engine / Calculation subsystem | Separate Derived Artifact catalog and namespace |
| Preview / edit session / Candidate | Editor and AI Assistant | Staging only; no formal write authority |
| Formal publication | Core Domain Domain Service | Validates, publishes Asset reference and updates revision pointer atomically |

A Field Dataset is a formal source-data authority inside Core Domain. It is not a second Map/World State authority. It supplies physical input to calculations and presentation; it does not replace semantic Objects, Map Initial State or History Commands.

## 4. Field Dataset domain model

### 4.1 Field Dataset

A Field Dataset represents one semantic field role over a declared Map Space. It has:

- stable Field Dataset ID;
- owning Map ID;
- stable field key from the built-in Field Descriptor Registry;
- Field Descriptor schema version and semantics ID;
- one current published Field Revision;
- creation and update audit metadata.

V1 enforces one authoritative Field Dataset per (Map, field key). Multiple candidate versions or imported alternatives remain staged until one is adopted as the current revision. A user cannot create two competing authoritative elevation datasets for the same Map without a future explicit multi-source/priority contract.

Field Dataset identity is not a raster tile, file name, geometry component, Object ID or derived-analysis result.

### 4.2 Field Revision

A Field Revision is an immutable snapshot of a Field Dataset's content and interpretation metadata.

It contains:
- stable Field Revision ID;
- owning Field Dataset ID;
- monotonic revision number within the dataset;
- optional parent revision ID for edit lineage;
- typed immutable Field Revision Asset reference;
- content fingerprint;
- canonical field manifest;
- source and derivation provenance;
- publication audit metadata.

Published revisions are never edited in place or reused. The current revision pointer may move to a newly published revision only after validation and a successful compare-and-swap transaction. Older revisions remain addressable while retained by audit, calculation lineage, checkpoints or other references.

Field Revision lineage is source-data provenance, not a second World History, Branch history or Command Revision Chain.

### 4.3 Built-in Field Descriptor

A static Core Domain registry defines field semantics. V1 starts with:

| field_key | Field kind | Value semantics | V1 purpose |
|---|---|---|---|
| elevation | continuous_scalar | finite numeric elevation with unit and vertical reference | Terrain shape and drainage source input |
| land_water | categorical_mask | registered land/water/unknown/outside categories | Land/water context and coastline derivation |

Other fields (soil class, infiltration, surface water, ice, vegetation, authored wind assumptions, etc.) require an explicit descriptor addition with typed values, units, no-data semantics, provenance and validation fixtures. Do not store them as arbitrary custom properties.

## 5. Field content and manifest contract

Every published Field Revision must have a canonical manifest that declares:

| Property | Requirement |
|---|---|
| field_key | Stable registered field role |
| field_schema_version / semantics_id | Exact descriptor compatibility |
| map_id / map_space_id | Owning Map and spatial frame |
| spatial_reference_id | Stable coordinate-reference identity; no EPSG-only assumption |
| coverage | Explicit extent and coverage geometry/manifest |
| map_edge_semantics | Closed boundary, wrap/seam, partial coverage or unknown |
| resolution_profile | Cell size/grid spacing, dimensions and resolution level |
| grid_alignment | Origin, axis, orientation and cell registration semantics |
| value_type | Registered finite numeric or categorical representation |
| unit | Required where the field is dimensional |
| vertical_reference_id | Required for elevation where applicable |
| no_data_semantics | Distinguishes no-data, unknown, outside coverage and valid zero |
| precision_profile | Storage precision, quantization and conversion rules |
| tile_manifest | Deterministic tile identity/order and coverage, if tiled |
| source_provenance | Authored, imported or explicitly adopted derived source |
| derivation_provenance | Required when a derived result is adopted as a source revision |
| content_fingerprint | Hash of canonical payload bytes and manifest compatibility |

No-data is never treated as zero, sea level, land, water, a sink or an outlet unless the field descriptor explicitly defines that value as valid data.

A Map edge, tile edge, viewport edge or partial-coverage edge is not automatically a coastline or physical boundary.

## 6. Scope and temporal semantics

### 6.1 Recommended V1 owner: Map Source Field Baseline

The Field Dataset belongs to exactly one Map. It is not Project-shared and is not owned by a Branch.

A Map may use the same immutable Asset bytes as another Map, including content-addressed deduplication, but each Map retains its own Field Dataset identity, revision lineage and current pointer. Updating one Map's Field Dataset does not update another Map.

### 6.2 Relationship to Map Initial State

Field Datasets are not Objects and must not be inserted into the Map Initial Object membership payload or represented as one Object per cell.

They form a separate Map Source Field Baseline owned by Core Domain. This avoids overloading Map Initial State, whose current contract describes time-zero Object membership, placement, initial attributes and relationships.

The Map Source Field Baseline is included in the Map's formal reconstruction/calculation input context, but it is not itself a Command or a State Change.

### 6.3 Branch and Historical Time behavior — adopted V1 contract

Under the recommended V1 model:
- every Branch of a Map reads the same current Map Source Field Baseline;
- a new Field Revision applies to the Map and all its Branches;
- it applies to every Historical Time because it corrects/replaces the Map's current base source data;
- it does not create a historical event, append a Command, or alter a Branch fork boundary;
- all dependent Map/Branch Checkpoints and Derived Artifacts are invalidated by the new Field Revision fingerprint.

This is deliberately different from Branch-local Object runtime state and post-zero Commands.

### 6.4 Deferred temporal field evolution

V1 does not support a source Field Dataset whose values vary by Historical Time or Branch. A geological event that changes terrain over time must not be implemented by replacing the current source field and pretending the change is historical.

A future time-varying field model requires an explicit typed Core Domain operation/State Change contract, captured immutable inputs, deterministic replay semantics and spatially bounded effects. It is not implied by this proposal.

## 7. Revision lifecycle

### 7.1 Authoring/import staging

1. User or AI stages a new field from brush editing, import or an explicitly approved derived output.
2. The editor pins the expected current Field Revision ID/fingerprint.
3. The staged content is validated for field key, spatial reference, coverage, resolution, units, vertical reference, no-data, precision and boundary semantics.
4. Any reprojection, resampling or quantization is an explicit named deterministic transformation with parameters and provenance.
5. The user reviews affected coverage and downstream impact.
6. The staged bytes remain provisional; no formal Field Dataset revision exists yet.

### 7.2 Publication

1. Core Domain preflight verifies Map ownership, field descriptor compatibility and expected current revision.
2. The exact payload is canonicalized and hashed.
3. Immutable Asset bytes are written and verified outside the short SQLite write transaction.
4. One short transaction inserts the immutable Field Revision record, links its typed Asset reference, advances the Field Dataset current revision pointer using compare-and-swap, and records the formal publication audit.
5. After commit, calculation freshness is resolved against the new revision fingerprint. Dependent artifacts become stale; unrelated artifacts remain current.
6. The editor reports “Published” only after Core Domain confirms the transaction.

If the database transaction fails after file publication, the Asset remains an unreferenced orphan for the established safe orphan-reconciliation process. File existence alone never implies successful publication.

### 7.3 Concurrent/stale update

Publication must include expected_current_revision_id. If the current pointer changed since the edit session began, Core Domain rejects the stale publication. The editor must rebase/revalidate the staged edit or ask the user to review the new base; it must not overwrite the newer revision.

## 8. Asset ownership and storage

### 8.1 Typed ownership

A Field Revision references immutable Core Asset content through a field-specific typed ownership relationship. Recommended logical physical model:

- core_map_field_dataset — stable Map-owned field identity and current revision pointer;
- core_map_field_revision — immutable revision metadata and provenance;
- a typed field_revision_asset_id reference from the revision to core_asset.

The reference is typed by its owning Field Revision row. Do not add a universal polymorphic Asset owner table or a generic Asset CRUD API.

### 8.2 Payload packaging

The logical contract treats one Field Revision as one immutable payload package. The package may internally contain a manifest and multiple tile payloads. The exact package format, compression, tile size, storage layout and random-access strategy remain Map Engine technical decisions.

Do not assume the full field is stored as one giant in-memory array or that one raster file format is already selected.

### 8.3 Asset retention

- A published Field Revision and its Asset bytes are immutable.
- Old revisions remain retained while referenced by audit, calculation input manifests, Checkpoints or other formal evidence.
- A revision cannot be garbage-collected while its Asset is referenced.
- Content-addressed deduplication may reuse identical bytes, but does not merge Field Dataset identity or ownership across Maps.

## 9. Relationship to Geometry, Derived Artifacts and rendering

| Content | Authority | Example |
|---|---|---|
| Semantic Object | Core Domain Object | Landmass, MountainRange, River, Lake |
| Vector Geometry Component | Core Domain + immutable Geometry Asset | Landmass boundary, River centerline, Lake shoreline |
| Source Field Dataset | Core Domain Field Dataset + immutable Field Asset | Elevation, land/water mask |
| Derived Analysis Artifact | Map Engine Calculation subsystem | Slope, flow direction, watershed, temperature, precipitation, biome suitability |
| Renderer SceneSnapshot/cache | Map Engine / Renderer | Projected geometry, raster texture, spatial index, GPU buffer |

Rules:
- A Landmass polygon and land/water mask are related but not interchangeable; disagreement is diagnosed, not silently reconciled.
- A River Object and a derived channel/flow field are separate.
- A MountainRange Object is not a raster component collection.
- Derived analysis does not become a source Field Dataset until the creator explicitly adopts the exact result through Core Domain.
- Renderer caches never become Field Revision authority.

## 10. Calculation and Checkpoint integration

Every calculation that reads a source field captures:
- Field Dataset ID;
- Field Revision ID;
- immutable Core Asset ID and content hash;
- field descriptor schema version and semantics ID;
- canonical field manifest fingerprint;
- Map Space, coverage, resolution and no-data semantics.

A new current Field Revision invalidates only calculations and Checkpoints whose resolved input manifest includes that dataset/revision. It affects every Branch and Historical Time of the owning Map under the adopted Map Source Field Baseline semantics.

A Derived Artifact retains the exact Field Revision input identity even after a newer revision becomes current. It becomes stale for current-map use but remains inspectable for audit/comparison.

Core Domain Checkpoint fingerprints must include the relevant Map Source Field Baseline fingerprint if the reconstructed state or its derived geometry depends on it. The Calculation subsystem retains its own artifact fingerprints; it does not replace Core Domain Checkpoint authority.

## 11. Validation contract

A Field Revision must be rejected when any of the following holds:

- Field key or descriptor version is unknown/unsupported.
- Map ownership does not match the target Map.
- Spatial reference is missing, invalid or incompatible with Map Space.
- Elevation lacks a valid unit or required vertical reference.
- Coverage, grid alignment or resolution metadata is malformed.
- No-data, unknown and outside-coverage semantics are conflated.
- Categorical data contains an unregistered category.
- Numeric payload contains NaN, infinity or invalid values.
- Tile manifest has duplicate, missing, overlapping or inconsistent tile identities where prohibited.
- Asset bytes do not match the declared content fingerprint.
- Provenance or required transformation parameters are missing.
- Publication's expected current revision is stale.
- The proposed payload attempts to mutate an already published Asset in place.

Warnings may include partial coverage, coarse resolution, uncertain import metadata, suspicious gradients, tile seams or resampling artefacts. A warning must not be silently promoted to a hard error unless the requested downstream calculation requires the missing quality guarantee.

## 12. Failure and recovery

| Failure | Required behavior |
|---|---|
| Import/brush preview cancelled | Discard staging only; formal Field Dataset unchanged |
| Validation failure | Do not publish; retain diagnostics and staging where safe |
| Asset write/hash failure | Do not create a Field Revision or advance current pointer |
| SQLite transaction failure after Asset publication | No revision/pointer change; orphan Asset handled by safe reconciliation |
| Compare-and-swap conflict | Reject stale publication; require rebase/review |
| Calculation failure after field publication | Keep the new formal source revision; keep last valid derived artifacts marked stale |
| Renderer/SceneSnapshot failure | Keep source Field Revision and successfully published Derived Artifacts; rebuild scene separately |
| Application restart during publication | Reconcile Asset files against committed revision references; never infer commit from file presence |

## 13. Recommended logical persistence shape (not executable DDL)

| Logical entity | Key fields | Invariants |
|---|---|---|
| Map Field Dataset | field_dataset_id, map_id, field_key, descriptor_schema_version, semantics_id, current_field_revision_id | Unique (map_id, field_key); exactly one Map owner |
| Field Revision | field_revision_id, field_dataset_id, revision_number, parent_revision_id, field_payload_asset_id, content_fingerprint, manifest_json, provenance_json, created_at | Immutable; unique (field_dataset_id, revision_number); Asset is immutable and same-Project |
| Core Asset | existing core_asset identity/hash/storage metadata | Reuse existing Core Asset authority; no generic field-only Asset registry |

The current revision pointer must be constrained to a revision belonging to the same Field Dataset. Exact SQLite FK ordering, deferred constraints and migration sequence belong in the P2-09-11 physical-schema update after this logical contract is adopted.

No separate Branch Field Dataset, Field Command table, field World State table or second history stream is introduced in V1.

## 14. Required verification vectors

| ID | Scenario | Expected |
|---|---|---|
| FD-001 | Create first elevation Field Dataset for Map A | One Map-owned identity and immutable revision |
| FD-002 | Create second authoritative elevation dataset for same Map | Rejected under one-field-key-per-Map V1 rule |
| FD-003 | Publish same bytes for Map B | Separate Field Dataset identity; Asset content may deduplicate |
| FD-004 | Update Map A current revision | Map B unchanged |
| FD-005 | Update current revision with stale expected revision ID | Compare-and-swap rejection |
| FD-006 | Mutate bytes of published Asset | Rejected / integrity mismatch |
| FD-007 | Field Revision references wrong-Project Asset | Rejected |
| FD-008 | Elevation missing unit or vertical reference | Rejected |
| FD-009 | No-data encoded as valid zero | Rejected unless field descriptor explicitly declares it valid |
| FD-010 | Land/water mask has unknown category | Rejected |
| FD-011 | Partial coverage is imported | Published only with explicit partial coverage and no-data boundary |
| FD-012 | Field revision changes on a Map with multiple Branches | All Branches resolve the new current Map baseline under proposed semantics |
| FD-013 | Reconstruct earlier Historical Time after Map field baseline update | Uses current Map field revision under proposed retroactive semantics |
| FD-014 | Branch-specific field override submitted in V1 | Rejected/deferred |
| FD-015 | Derived Artifact displayed without adoption | Does not create a formal Field Dataset revision |
| FD-016 | Derived output explicitly adopted as source field | Exact accepted bytes published through Core Asset and Field Revision path |
| FD-017 | SQLite failure after Asset publication | No Field Revision/current pointer commit; orphan Asset safely reconciled |
| FD-018 | Field revision changes | Dependent calculations and relevant Checkpoints become stale; unrelated outputs remain current |
| FD-019 | Source field edit changes viewport only | Rejected as formal edit; viewport is not field scope |
| FD-020 | Field resampling occurs | Method/version/parameters and source fingerprint retained |

These are design vectors only; they have not been executed.

## 15. Confirmed product-semantic decision

The V1 model treats source physical fields as a **Map Source Field Baseline**. The product owner confirmed on 2026-11-21 that publishing a new Field Revision changes the current base input for all Branches and all Historical Time points of that Map. It does not represent a historical terrain change.

The accepted decision is grounded in:
- source field edits in ME-02 are primarily authoring corrections/refinements to the map's physical base;
- the current History model is Command-driven and does not yet define temporal raster field state;
- Branches should not silently become separate physical-field copies;
- the model avoids a second time-series history and keeps calculations reproducible.

If the product must support “the same Map has different elevation/land-water fields at different historical times” in V1, this proposal is not sufficient; a typed time-varying field-state model must be designed instead. That is a distinct functional capability, not a storage toggle.

**Decision: ACCEPTED.** V1 Field Revision publication is Map-wide and retroactive across every Branch and Historical Time of the owning Map. Another Map is unaffected, even if it reuses identical immutable Asset bytes. This is a source-baseline correction/update, not a historical event. No Command, State Change, Branch fork-boundary change or per-Branch copy is created.

## 16. Status and next steps

This adopted contract defines the formal authority and logical persistence boundary for H-G01. The product-semantic gate is closed. Physical-schema, descriptor/validation and provider-proof design artifacts are documented; package/tile logical design is documented in ME-05-07 V0.1; ME-05-08 V0.1 now specifies the byte-level header/directory and P2-09-33 provides 28 design vectors; parser/writer implementation, codec/precision selection, implementation and actual provider/platform test execution remain open.

Follow-up disposition:
1. **Completed as design artifacts:** P2-09-11 V0.11 physical schema reconciliation; P2-09-12 V0.6 provider proof specification; P2-09-30 Field Descriptor Registry V0.1; P2-09-31 Field Dataset validation vectors V0.1; ME-05-07 Field Payload Package & Tiling Contract V0.2; ME-05-08 Field Package Binary Envelope & Directory Specification V0.1; P2-09-33 Field Package Binary Envelope Validation V0.1; ME-05-05 Geometry & Field Asset Package/Publication V0.2; P2-09-32 Field Package/Tiling validation vectors V0.1; ME-02-F V0.4 calculation package-input/freshness alignment.
2. **Open:** implement and test the ME-05-08 header/directory parser and writer; benchmark tile dimensions and select codec/precision profiles under ME-05-07 V0.2.
3. **Open:** implement Core Domain Field Dataset/Revision services, typed Asset ownership, publication compare-and-swap and safe orphan recovery.
4. **Open:** execute Windows/macOS provider/runtime publication, recovery, integrity and Checkpoint-invalidation tests.
5. These documents remain design artifacts. No executable DDL, production implementation or passing test result is implied.

No implementation, executable DDL or test pass is implied by this document.
