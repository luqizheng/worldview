# ARCH-01 — Product-Owner Decisions Accepted

- Version: 1.0
- Date: 2026-11-27
- Status: **Accepted**
- Decision authority: Product owner confirmation in the project conversation on 2026-11-27
- Architecture audit: [ARCH-01 cross-domain dependency and reconciliation](ARCH-01-cross-domain-dependency-and-reconciliation-v0.1.md)
- Proposal superseded: [ARCH-01 decision gate proposal](ARCH-01-decision-gate-v0.1.md)

This document records the four binding product decisions that close the ARCH-01 decision gate. It supersedes the proposal status and any alternative left open in the proposal. Implementation must follow these decisions; it must not silently reinterpret them.

## 1. First authorable Object Types — ACCEPTED

The first V1 point/line/area authoring slice uses the existing built-in descriptors:

| User-facing geometry | Object Type | Required geometry role | Geometry family |
|---|---|---|---|
| Point | `point_of_interest` | `location` | `point` |
| Line | `river` | `centerline` | `line_string` / `multi_line_string` |
| Area | `landmass` | `extent` | `polygon` / `multi_polygon` |

Only each selected descriptor's required geometry role is in the first slice. Do not create generic UI-defined Object identities. Additional roles, relationships, river/hydrology semantics, elevation, split/merge and arbitrary attributes remain out of scope unless separately approved.

## 2. New Map default Spatial Reference — ACCEPTED

Every newly created Map receives an immutable 2D Local Cartesian Spatial Reference:

- Kind: `LocalCartesian`
- Dimension: 2
- Axis order: XY
- Coordinate unit: `world-unit`, explicitly abstract and not a physical unit
- Wrap behavior: None
- Vertical reference: absent
- Origin: Map-local; geometry never stores screen pixels
- Provenance: built-in World View default profile, versioned and fingerprinted in the Project

The exact profile identifier, numeric precision/tolerance, valid coordinate extent and user-facing measurement wording remain design inputs for DESIGN-EXTENT / PRA-03. They must be explicitly specified and tested before authoring is enabled. Do not silently substitute WGS84, longitude/latitude, metres or feet.

The Spatial Reference definition is immutable. Changing it after geometry exists requires a separately designed and validated coordinate-conversion operation; it is not an in-place metadata edit.

## 3. Temporal authoring and formal Undo/Redo — ACCEPTED

The first V1 manual authoring workflow edits the Map's **time-zero Initial State**.

The owning Core Domain must provide a durable baseline revision/edit-operation model. The existing mutable compare-and-swap full-set Initial State baseline is not sufficient by itself.

**Contract supersession:** this accepted decision explicitly supersedes the earlier no-revision-chain/no-revision-table direction recorded in `docs/core-domain/CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md` §10 and its cited P2-09-07 / P2-09-16 baseline assumptions. Those documents remain historical design records; their Initial State ownership and time-zero-versus-post-zero boundary remain valid, but their prohibition on durable baseline revisions is no longer authoritative. The implementation contract must be amended before code is changed.

The formal model must provide:

1. Immutable, durable revisions or equivalent formal baseline edit operations.
2. Deterministic Undo and Redo owned by Core Domain, not a UI-only command stack.
3. Atomic publication across immutable geometry assets, stable Object identity/registration and Initial State membership.
4. Stable identity allocation; undo reverts/removes baseline membership and does not recycle identity history.
5. Durable active revision/cursor recovery after application close and project reopen.
6. Stale-revision rejection and all-or-nothing failure behavior; failed publication must not leave partial formal state.
7. Clear separation from post-zero Map/Branch History. A time-zero edit is not represented as a historical Command.

The first implementation must not route these edits through non-zero History Commands or invent an implicit later authoring date. The formal baseline revision model is a V1 acceptance prerequisite for editing and Undo/Redo.

## 4. Layer ownership — ACCEPTED

Initial V1 layer/group membership, ordering, visibility, expanded/collapsed state and active-layer selection are **Workspace-local Presentation state**.

- Core Domain remains the authority for Object existence and semantic relationships.
- Layers are not Object Types, semantic relationships or a second Object ownership registry.
- Do not add Core Domain tables for cosmetic layer organization.
- Persist workspace preferences locally only through the appropriate existing workspace persistence boundary.
- The first slice needs usable groups/layers, visibility toggling and deterministic ordering, not a general-purpose GIS layer-management framework.

## 5. Implementation and sequencing consequences

These decisions close the ARCH-01 product-owner decision gate. They do not claim that the capabilities are already implemented.

The execution sequence must:

1. Refresh the source baseline and finish FOUNDATION-01's Project/Map/Spatial Reference/Application gap analysis.
2. Finalize the default Spatial Reference profile's precision and extent through DESIGN-EXTENT before enabling geometry authoring.
3. Design the Core-Domain-owned baseline revision model and its atomic Asset/identity/membership boundary before implementing V1 editing or Undo/Redo.
4. Keep layer state in the Workspace Presentation boundary.
5. Implement the accepted Object Type slice through the existing descriptor catalog and typed schema.
6. Preserve the dependency order in `plan.md`; do not start PRA-05 optimization before correctness and representative fixtures are proven.

This record is a product decision, not an implementation report. Each capability may be marked complete only after code, automated tests, independent review, integration and Windows/macOS evidence meet the repository's work-package rules.
