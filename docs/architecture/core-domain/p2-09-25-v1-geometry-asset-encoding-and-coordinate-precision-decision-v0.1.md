# World View P2-09-25 — V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended V1 direction with explicit precision/format validation gates; not a frozen ADR or implementation authorization
- Depends on: P2-09-11 V0.10; P2-09-12 V0.4; P2-09-17 V0.5; P2-09-19 V0.2; P2-09-21 V0.1; ME-01 spatial contracts

## 1. Decision summary

Recommend a **versioned canonical JSON geometry payload** as the V1 authoritative interchange/Asset representation, with an explicit World View geometry envelope rather than unmodified RFC 7946 GeoJSON.

Reasons:
- fictional and local maps cannot be forced into WGS 84 longitude/latitude;
- coordinate reference identity, units, axis order, dimension and precision must be explicit;
- V1 requires Point, LineString, Polygon, multipart and authored curve support;
- JSON is inspectable, diffable and maintainable for a local-first single-user application;
- immutable Asset hashing and deterministic canonical serialization fit the existing Asset Store model.

This is a recommendation, not a claim that JSON is the final high-performance renderer format. Renderer meshes, spatial indexes, hit-test structures and LOD remain derived caches.

## 2. Canonical Asset envelope

The immutable Asset should contain:
- `format_id = worldview.geometry-json`;
- `format_version` (independent from Object Type and Command payload versions);
- `spatial_reference_id`;
- `axis_order` and coordinate `unit`;
- `dimension` (2D or 3D);
- `geometry_family`;
- `coordinate_precision_profile_id`;
- canonical geometry body;
- optional source/provenance manifest.

The Core Domain owner payload separately pins component ID, role, Asset ID, content hash and applicable schema/semantics IDs. The Asset must not duplicate mutable Object metadata.

## 3. Geometry body and curves

Use a bounded typed geometry body:
- Point: one coordinate tuple;
- LineString: ordered coordinate tuples;
- Polygon: explicitly closed rings with declared exterior/interior roles;
- MultiPoint/MultiLineString/MultiPolygon: ordered member geometries;
- Curve: explicit segment sequence, including line and cubic Bézier segments with control points.

Curve segments are formal authoring geometry; tessellation/flattening is derived and renderer-specific. V1 should reject unknown segment types rather than silently linearize them.

Ring orientation and canonical ordering must be defined by the geometry schema version. The serializer must not reorder authored path direction where it carries meaning (for example, river centerline digitization), and must not treat that direction as hydrological flow direction.

## 4. Coordinate reference model

A spatial reference is an immutable, project-scoped definition identified by stable ID. It must state:
- reference kind: Geographic, Projected, or LocalCartesian;
- coordinate unit and axis order;
- origin and basis/transform parameters where local;
- datum/ellipsoid/projection parameters where geographic/projected;
- valid coordinate domain and wrap/discontinuity policy;
- optional vertical reference for 3D coordinates.

Do not require EPSG codes. An EPSG identifier may be a recognized external alias, but the complete World View spatial-reference definition remains available locally and immutable once referenced by formal geometry.

A coordinate-reference definition change creates a new identity/version and requires an explicit conversion operation that publishes new geometry Assets; it must not silently reinterpret existing coordinate tuples.

## 5. Precision recommendation

Do not select one global decimal-place count for all maps. Instead:
- define a per-spatial-reference precision profile;
- store coordinates as canonical decimal numbers, never binary floating-point text generated from locale formatting;
- specify maximum absolute quantization error in the coordinate unit for each profile;
- specify rounding mode and overflow/domain validation;
- preserve the original authored precision within the profile's declared tolerance;
- reject non-finite values and values outside the reference domain.

For 2D local maps, the initial profile should be chosen from the map's declared world extent and smallest meaningful feature size, with quantization error no greater than 1/10 of the minimum supported authoring tolerance. The exact numeric value remains a validation output, not a guessed universal constant.

For geographic coordinates, use an angular precision profile with an explicit maximum ground error at a declared latitude/domain; do not assume a fixed decimal count has uniform metric meaning.

3D elevation precision and vertical reference conversion remain deferred to ME-04/ME-05.

## 6. Format comparison

| Candidate | Strength | Limitation for V1 | Recommendation |
|---|---|---|---|
| RFC 7946 GeoJSON | Familiar, inspectable, broad tooling | WGS 84 longitude/latitude assumptions; no first-class authored curves; coordinate context insufficient for local fictional maps | Use only as import/export interchange with explicit transform |
| WKB/EWKB | Compact, mature geometry encoding | Coordinate reference and curve extension interoperability vary; less inspectable; canonical byte rules need extra specification | Defer as optional derived/optimized representation |
| FlatGeobuf | Spatially indexed and efficient for larger vector datasets | Dataset-oriented, not ideal as one small immutable component Asset; curve and local CRS constraints need evaluation | Consider for bulk import/export or derived spatial query cache |
| Custom JSON envelope | Explicit CRS, curve, role-independent geometry, inspectable and hashable | Larger and requires a strict serializer/validator | Recommended authoritative V1 Asset format |

## 7. Determinism and hashing

- Canonical UTF-8 JSON; ordinal object-key ordering; invariant decimal serialization; no NaN/Infinity.
- Arrays preserve semantic order unless the geometry schema explicitly defines an order-insensitive collection.
- Hash the exact canonical Asset bytes using the Core Asset hash algorithm/version.
- Serializer changes that alter bytes require a format version change or an explicit canonicalization migration.
- Asset publication is immutable. An edit publishes a new Asset and preserves the Geometry Component identity.
- Derived caches carry source Asset hash, transform/renderer algorithm version and parameters; they are disposable.

## 8. Validation gates

1. Implement-independent fixture corpus for all geometry families, ring rules, curves, multipart ordering, axis order, wrap behavior and invalid values.
2. Measure JSON size and parse/validation time on representative world-scale and detailed local geometry.
3. Verify deterministic byte output across Windows and macOS runtimes.
4. Compare precision profiles against representative smallest feature sizes and map extents.
5. Validate round-trip import/export for GeoJSON and selected external formats without silently losing curves or CRS.
6. Confirm the selected .NET geometry library can preserve authored curves and coordinate context without becoming the Core Domain authority.

## 9. Status

This document recommends a canonical JSON Asset envelope for V1 because it is the simplest inspectable fit for the current domain and local-first operating model. The exact JSON geometry grammar, numeric precision profiles, curve operations and provider/runtime performance remain validation gates. No renderer or geometry library is selected by this decision.
