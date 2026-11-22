# Map Space and Spatial Reference Ownership Contract V1

- Status: Implemented in PR #11: Domain records/rules, SQLite adapter, EF mapping and generated migration; precision-profile fixture acceptance remains an implementation gate
- Date: 2026-11-22
- Scope: Map Space identity, Map-to-Spatial-Reference binding, immutable project-scoped Spatial Reference definitions, and Field Revision validation
- Depends on: ME-01-04 V0.1; ME-05-01 V0.5; ME-05-04 V0.1; P2-09-30; P2-09-34

## 1. Purpose

Resolve the ownership boundary needed by Field Revision publication without inventing a separate GIS hierarchy or confusing Map identity, Map Space, and Spatial Reference.

The governing distinctions are:

- Map is the Core Domain aggregate/owner.
- Map Space is the continuous spatial domain belonging to one Map.
- Spatial Reference is the immutable coordinate interpretation definition used by that Map Space.
- Grid and Field Revision are datasets defined inside that Map Space; they do not own or redefine the Map's coordinate reference.

## 2. Map Space ownership

ME-01-04 adopts one continuous Map Space for one Map, with World/Region/Locality/Site treated as working scopes rather than separate spatial databases or independent Map Spaces.

Therefore V1 uses these rules:

1. Each Map has exactly one stable Map Space identity.
2. The Map Space identity is stored as a typed `map_space_id` property on the owning `core_map` row. It is not a separate independently mutable aggregate/table in V1.
3. `map_space_id` is distinct from `map_id`; it is generated once at Map creation and remains immutable.
4. A Map Space is not a viewport, working scope, Map Extent, zoom level, tile set, or renderer coordinate cache.
5. Multiple Field Datasets in one Map share the same Map Space identity but may have different grids, coverage and resolution.
6. A Map Space cannot be shared between Maps in V1. Identical source bytes may be reused, but each Map retains its own Map Space and Field Dataset ownership.
7. Future multi-space Maps require a separate explicit product/domain decision; they are not implied by this V1 contract.

The Map Space ID in a Field Revision manifest must exactly match the owning Map's `map_space_id`.

## 3. Spatial Reference ownership

ME-05-04 defines a Spatial Reference as an immutable, project-scoped definition that remains locally available once referenced. V1 persistence should reflect that:

1. Add a project-scoped `core_spatial_reference` definition record.
2. Each definition has a stable `spatial_reference_id`, owning `project_id`, immutable definition version, reference kind (`Geographic`, `Projected`, or `LocalCartesian`), coordinate unit, axis order, valid domain, wrap/discontinuity behavior, dimensionality, optional vertical-reference identity, coordinate precision-profile identity, definition parameters, provenance and canonical content fingerprint.
3. The typed fields above are queryable and constrained. Projection/datum-specific parameters may use a bounded, versioned JSON extension until their typed vocabulary is justified; the extension must not replace the typed identity, units, axis, domain, version or hash.
4. A project may have multiple Spatial Reference definitions; multiple Maps may reuse the same immutable definition.
5. A Map may exist during initial setup without a Spatial Reference binding. Before any Field Revision can be published, it must have exactly one active binding. The binding is stored on `core_map` as `spatial_reference_id`, with a same-Project restrictive foreign key to `core_spatial_reference`. The column is nullable only for this explicit unconfigured state.
6. Changing a Map's active Spatial Reference is a formal conversion/migration operation, not an in-place edit to the referenced definition. Existing immutable Assets and Field Revisions retain their original reference identity.
7. Deleting a Spatial Reference that is referenced by a Map or immutable Asset/Revision is prohibited. Definitions are retired, not mutated or hard-deleted.

A Field Revision's `spatial_reference_id` must match its owning Map's active Spatial Reference at publication time. The revision still snapshots the ID so its interpretation remains immutable after a later Map-level conversion.

## 4. Vertical reference and coordinate precision

### 4.1 Vertical reference

ME-05-04 describes an optional vertical reference as part of the Spatial Reference definition; P2-09-30 requires elevation to pin a stable `vertical_reference_id`.

V1 therefore stores the optional vertical-reference identity in the immutable Spatial Reference definition rather than creating an independent global vertical-reference catalog prematurely.

- The identity is stored in the immutable Spatial Reference definition and is validated against the elevation manifest at publication.
- The canonical Field manifest must include `spatial.vertical_reference_id` and it must exactly match the Spatial Reference definition.
- Elevation without a vertical reference is invalid for new formal publication.
- `unit` (m or ft) is the numeric elevation sample unit. It is not a vertical datum/reference and does not replace `vertical_reference_id`.
- Land/water categorical fields omit both elevation unit and vertical reference.
- If a future requirement needs independently shared vertical reference definitions, separate versioned entities may be introduced through an explicit migration/ADR; V1 does not assume that need.

### 4.2 Coordinate precision profile

ME-05-04 states that precision is selected per Spatial Reference and authoring scale.

- The immutable Spatial Reference definition owns the selected `coordinate_precision_profile_id` and its versioned profile values.
- The canonical Field manifest snapshots that ID in `spatial.coordinate_precision_profile_id`.
- The ID in the manifest must match the profile selected by the referenced Spatial Reference definition.
- Coordinate precision is not the same as elevation sample precision or `value_encoding.profile_id`.
- Do not invent numeric precision thresholds. The profile must be selected from representative World/Region/Locality/Site fixtures and validated against the minimum supported authoring tolerance and smallest meaningful feature size.
- The current precision values remain an implementation gate; a non-empty ID alone does not prove the profile is adequate.

## 5. Field Revision owner-context validation

For every new formal Field Revision publication, Core Domain must verify in one consistent owner context:

1. The Project and Map exist and the Map belongs to the Project.
2. The Map has a non-empty stable `map_space_id`.
3. The Map's Spatial Reference exists, belongs to the same Project and is immutable/available.
4. Manifest `spatial.map_space_id` equals the Map's `map_space_id`.
5. Manifest `spatial.spatial_reference_id` equals the Map's active `spatial_reference_id`.
6. Manifest `spatial.coordinate_precision_profile_id` equals the profile pinned by that Spatial Reference definition.
7. For elevation, manifest `spatial.vertical_reference_id` equals the optional vertical-reference identity in that Spatial Reference; absence is a validation failure.
8. For elevation, `field.unit` is one of the descriptor-approved units.
9. Grid coordinates, resolution and alignment are validated against the Spatial Reference's coordinate unit, axis order, domain and precision profile. The validation must not silently transform or round coordinates.
10. Existing published revisions are immutable and retain their original snapshot IDs.

The initial release has no historical user data. Every newly published Field Revision must satisfy the complete canonical manifest and owner-context contract. The Domain service and SQLite transaction both validate the Map Space, active Spatial Reference, precision-profile identity and vertical-reference identity.

## 6. Proposed relational shape

The first EF schema iteration should add:

- `core_map.map_space_id`: required unique stable identity.
- `core_map.spatial_reference_id`: nullable only until the initial binding is established; same-Project FK enforcement applies whenever it is non-null.
- `core_spatial_reference`: immutable project-scoped reference definition records, keyed by (project_id, spatial_reference_id), with typed reference/profile fields and a canonical content fingerprint.
- Restrictive FKs from Field Dataset/Revision owner context to Map/Spatial Reference where the exact existing schema permits; keep revision snapshot IDs for immutable interpretation.
- No standalone `core_map_space` table in V1.
- No independent vertical-reference table in V1; its immutable definition is carried by the Spatial Reference record.
- No hard-delete cascade for spatial reference definitions.

Implemented EF migration: `20261122140144_MapSpatialReferenceOwnership`. It was generated by `dotnet ef migrations add MapSpatialReferenceOwnership`; the generated Designer and `CoreDomainDbContextModelSnapshot` were committed by the CLI workflow. Decimal precision-profile values are persisted as invariant text to avoid SQLite binary-float conversion.

## 7. Initial database and Map creation

- New Map creation generates one independent stable Map Space ID.
- The Map is initially spatially unconfigured (`spatial_reference_id = NULL`) until an explicit existing/new project-scoped definition is selected and bound through `MapSpatialReferenceService.BindInitial`.
- The initial binding is one-time. Replacing an existing binding is rejected; a future change requires a coordinate-conversion workflow.
- Field Revision metadata must reference the Map's established Map Space and Spatial Reference definitions at publication.
- Do not infer an EPSG code, Earth datum, unit, vertical datum or precision profile from a Map name, fixture string or filename.
- A Map without a resolvable Spatial Reference definition cannot publish a Field Revision.
- The initial release has no historical user data; no backfill or compatibility dual-read is implemented.

## 8. Acceptance tests

- One Map has exactly one stable Map Space ID; two Maps cannot share it.
- Multiple Field Datasets within one Map share the Map Space ID but retain independent grids/resolution.
- Multiple Maps in one Project may share one immutable Spatial Reference definition.
- Cross-Project Spatial Reference binding is rejected.
- A Map cannot publish a Field Revision whose Map Space or Spatial Reference differs from its owner context.
- Elevation requires a vertical reference that matches the immutable Spatial Reference definition.
- Land/water does not acquire elevation unit/vertical-reference semantics.
- Coordinate precision profile identity must match the Spatial Reference; profile values are typed and positive, while adequacy remains gated on representative fixtures rather than guessed thresholds.
- The Domain persistence port exposes Spatial Reference definition insert/read only; there is no edit/delete operation. Direct-SQL immutability triggers remain a separate hardening item.
- Changing the Map's active Spatial Reference requires a conversion workflow and does not reinterpret old revisions.
- No legacy-data compatibility path or historical-data backfill is required for the initial release.

## 9. Implementation order

1. **Completed:** typed immutable Spatial Reference and Coordinate Precision Profile Domain records, validation and fingerprint rules.
2. **Completed:** Map Space identity generation, initial Map binding service and no-rebind rule.
3. **Completed:** SQLite adapter and EF mapping for `core_spatial_reference`, Map Space identity and same-Project Spatial Reference FK.
4. **Completed:** generated migration `20261122140144_MapSpatialReferenceOwnership` and model snapshot.
5. **Completed:** Field Revision owner-context validation in Domain and transaction-level persistence revalidation.
6. **In progress:** representative precision fixtures, geographic ground-error policy and remaining precision-profile validation details; no numeric thresholds are guessed.
7. **Next:** typed Field Revision scalar/child metadata persistence, followed by Field Payload BLOB/managed-file storage and package writer fixtures.
