# FOUNDATION-01C — Geometry Asset Publication Design v0.1

- Date: 2026-11-28
- Status: SOURCE AUDIT COMPLETE; DESIGN PROPOSED FOR INDEPENDENT REVIEW
- Baseline: `671cd86254e7a2e4719423789f5c785b4ac7803e` (master, merge of PR #54)
- Parent contract: `docs/core-domain/FOUNDATION-01-time-zero-baseline-contract-v1.0.md`
- Parent implementation: `docs/architecture/FOUNDATION-01B-durable-baseline-revision-implementation-v0.1.md`
- Accepted product decisions: `docs/architecture/ARCH-01-decisions-accepted-v1.0.md`

## 1. Objective and boundary

Implement the first Map time-zero Geometry Asset publication path for the accepted semantic Object types:

| Object Type | Required role | Permitted geometry |
|---|---|---|
| `point_of_interest` | `location` | `point` |
| `river` | `centerline` | `line_string`, `multi_line_string` |
| `landmass` | `extent` | `polygon`, `multi_polygon` |

The authoritative result is one immutable complete Initial State revision whose membership payloads contain Geometry Component manifests and whose relational typed references resolve every manifest component to a verified immutable Core Asset.

This package does not implement post-zero Commands, geometry split/merge, hydrology/elevation, new semantic relationships, a generic component registry, renderer projection, hit-testing, or numeric precision/tolerance/valid-extent policy. It does not enable Building/Cave Definition publication through the Map Initial State path; their existing Definition owner remains separate.

## 2. Baseline source audit

The audit was performed against master `671cd86254e7a2e4719423789f5c785b4ac7803e`, after PR #54 was merged.

### 2.1 Durable revision foundation now exists

PR #54 provides:

- `core_map_initial_state` baseline identity;
- immutable `core_map_initial_state_revision` rows;
- complete immutable `core_map_initial_state_revision_member` snapshots;
- mutable `core_map_initial_state_cursor`;
- atomic Map creation of baseline/revision zero/cursor;
- atomic Object insertion of registration, definition, optional Initial State membership, revision append, identity activation and reservation transition;
- transaction-local CAS validation and deterministic revision fingerprint/operation-payload recomputation.

The member record currently carries Object identity, membership JSON, placement JSON and placement schema version. It has no typed Geometry Asset references. The append plan likewise carries complete members and fingerprints but no geometry-link set. The current fingerprint composition is `initial-state-fingerprint-v1`.

### 2.2 Geometry manifest and byte-store foundations exist, but in a different owner context

Existing reusable pieces:

- `GeometryComponentManifest` and `GeometryRules` validate component identity, descriptor role/family, 2D dimension, spatial-reference identifier, schema version, Asset ID and hash syntax.
- `GeometryRules.ValidateManifestCollection` validates duplicate component IDs and descriptor role cardinality.
- `GeometryAssetEnvelope` validates the bounded JSON geometry envelope and family/body shape.
- `IAssetStore` / `FileAssetStore` publish immutable content-addressed bytes outside SQLite and verify SHA-256.
- `AssetService.RegisterAsset` registers Core Asset metadata; `MarkAssetVerified` records byte-verification evidence.
- `ObjectDefinitionService.PublishDefinitionWithGeometryAssets` and `core_object_definition_asset` already implement atomic Building/Cave Definition + complete typed-link publication.

These are not the required Map time-zero publication path. The existing Building/Cave flow owns Project-shared/current Definition content; it does not attach geometry references to a Map revision. The existing Definition link table cannot be reused as a Map revision owner link without losing revision/Object/component context.

### 2.3 Missing capability confirmed

No current production table or port records the relation:

`Project + Map + Initial State + Revision + Object identity + Geometry Component + role + Asset`.

Consequently, a Map revision can currently persist membership JSON but cannot formally prove that every geometry manifest points to a same-Project verified Asset, nor preserve per-revision Asset ownership for old revisions.

### 2.4 Relevant existing contracts

- FOUNDATION-01 time-zero contract §2–§9: accepted types, geometry ownership, immutable assets, atomic publication, Map SR and failure behavior.
- P2-09-19 V0.2: component identity is in the typed owner payload; no generic `core_geometry_component` registry table.
- CD-02-G3: Building/Cave Definition link semantics only. Its generic `geometry_component` link-purpose role is not automatically the Map-revision schema.
- ME-05-05 V0.3 and its later revisions: immutable asset publication and separation of source Assets from derived presentation data.
- DESIGN-EXTENT / PRA-03 remain the authority for precision profiles, tolerances and valid extent.

## 3. Accepted design proposal

### 3.1 Geometry manifest ownership

For POI/River/Landmass time-zero state, the Geometry Component manifest is embedded in that Object's canonical `InitialObjectWrite.MembershipPayloadJson` under the typed `geometry_components` field.

Rationale:

- time-zero geometry is Map-specific Initial State, not a retroactive Project-shared Definition;
- the complete revision snapshot already owns membership payloads;
- component identity and component semantics remain in the typed owner payload;
- no second component registry or parallel mutable geometry owner is introduced.

The manifest uses the existing common fields: component ID, geometry role, family, dimension, Spatial Reference ID, geometry schema version, Asset ID, content hash, provenance and validity metadata. Building/Cave-only child-context fields are not imposed on POI/River/Landmass.

### 3.2 Revision-scoped typed link table

Add `core_map_initial_state_revision_asset`, one row per Geometry Component per immutable revision. Proposed columns:

- `revision_asset_id` primary key;
- `project_id`, `map_id`, `initial_state_id`, `revision_id`;
- `object_identity_id`, `geometry_component_id`, `geometry_role`;
- `asset_id`, `created_at_utc_ms`.

Required constraints:

- composite FK to the exact revision-member row, so a link cannot exist without that Object in that revision;
- composite FK to `core_asset(project_id, asset_id)`, so cross-Project references are impossible;
- unique key on (revision, Object identity, Geometry Component identity);
- stable checks for non-empty identities and supported role vocabulary;
- indexes for revision snapshot resolution and Asset-reference reconciliation.

The link is per component, not per distinct Asset. Two components may reference the same immutable Asset and therefore have two component-specific rows. This preserves the complete component-to-Asset mapping and avoids losing component identity in a generic owner/Asset pair. The manifest remains the semantic authority; the relational row is its typed integrity projection.

Do not add `core_geometry_component`, a generic polymorphic owner table, or a second Object registry.

### 3.3 Exact-set validation

The Domain derives the complete typed-link set from the canonical membership payloads. Callers do not supply a partial or independently mutable link list.

Before commit, the adapter re-reads and validates within the same SQLite transaction:

1. Map, baseline, expected active revision and cursor version;
2. every member's Object identity, active lifecycle, Map registration and pinned descriptor;
3. every manifest's unique component identity, declared required role, permitted family, 2D dimension and schema version;
4. manifest Spatial Reference equals the Map's immutable bound Spatial Reference;
5. every referenced Asset belongs to the same Project, is `Verified`, and has the exact manifest content hash;
6. every component maps to exactly one typed link row and no extra link exists;
7. component identity is retained for an existing component and is not reused after removal;
8. the complete child member snapshot, complete typed-link set, revision metadata and cursor move are committed together.

The persistence adapter must re-derive the expected links from the exact canonical member rows inside the transaction; it must not trust a Domain pre-read or a caller-provided link list.

### 3.4 Object insertion is part of the same gate

The current `InsertObject` path can append an Initial State membership revision atomically, but it has no geometry Asset references. For a descriptor whose required geometry role has minimum cardinality greater than zero, Object insertion must accept a complete geometry-bearing Initial Membership and publish its revision-member row plus typed Asset links in the same existing Object insertion transaction.

It must not create an active registered Object with a formally incomplete required geometry membership and rely on a later repair transaction. Existing callers that create an Object without Initial Membership remain governed by their current explicit use-case semantics; the first-slice POI/River/Landmass authoring path must satisfy the descriptor before formal insertion is committed.

### 3.5 Append and historical snapshot semantics

Every revision stores a complete snapshot and its complete typed Geometry Asset link set. A new revision does not inherit link rows from its parent.

- Existing components retain their component ID when their geometry changes; a changed payload is a new immutable Asset.
- Removed components are absent from the child revision and its link set; prior revisions retain their references.
- An Asset shared by multiple components remains referenced once per component row.
- Undo/Redo moves only the existing revision cursor. It does not copy, rewrite or republish Asset bytes or link rows.
- Abandoned revision descendants and their Asset references remain addressable.

### 3.6 Fingerprint composition v2 without rewriting immutable v1 history

Introduce a new revision content/fingerprint composition version for geometry-bearing snapshots. New revision writes use baseline content schema version 2 and `initial-state-fingerprint-v2`. The v2 composition includes:

- the existing canonical, identity-sorted membership and placement content;
- canonical Geometry Component manifest content;
- a deterministically sorted component-to-Asset reference projection (Object identity, component ID, role, Asset ID and pinned content hash).

Existing revision rows remain immutable v1 and are validated with the v1 algorithm. Existing Maps can therefore retain revision zero and historical revisions without rewriting fingerprints or operation metadata. Their first new revision after this change is v2; new Maps start with v2 revision zero. Revision lineage remains the parent ID, not schema version or ordinal.

The implementation must make the version dispatch explicit and test mixed v1-parent/v2-child history, Undo/Redo, branch creation and reopen. No migration may silently rewrite committed v1 revision/member rows.

### 3.7 File publication and database transaction boundary

Application flow:

1. Validate and canonicalize geometry input and manifest draft.
2. Publish bytes through `IAssetStore.PutAsync`; the store computes SHA-256 and atomically publishes immutable bytes.
3. Register or resolve the Core Asset metadata row and obtain byte-verification evidence; only a Verified Asset may be referenced.
4. Invoke the Domain append/Object-insert operation with the canonical membership intent and expected revision/cursor credentials.
5. In one short SQLite transaction, re-read all mutable facts, verify the Asset metadata/hash/integrity, derive the full typed links, write the complete revision/member/link set and update the cursor (or perform the corresponding atomic Object insertion).
6. Commit once.

No file I/O, geometry processing or asynchronous wait occurs inside the SQLite write transaction. If the DB transaction fails, no revision/member/link/cursor mutation survives. Immutable bytes and an unreferenced Core Asset metadata row may remain; they are orphans, not formal Map geometry. Existing safe Asset reconciliation must be extended to account for revision links before any cleanup is allowed.

## 4. Precision policy boundary

The default Map Spatial Reference remains 2D Local Cartesian / XY / abstract `world-unit` / Map-local origin. This package must not invent coordinate precision, quantization, tolerances, valid extent or physical units.

Geometry publication must fail closed unless the payload's precision-profile reference is accepted by the Map's bound Spatial Reference policy. The optional precision-policy storage added in FOUNDATION-01A is not, by itself, proof that a usable profile or numeric rules have been adopted. DESIGN-EXTENT must define the profile semantics before production authoring is enabled. 01C may implement the typed publication infrastructure and test it with explicit test policy fixtures, but must not install a fake/default production profile.

## 5. Migration and development-data handling

The schema migration adds the revision-scoped typed link table and indexes/constraints. It does not rewrite immutable v1 revisions, their members, fingerprints or operation payloads. Existing revisions have an empty geometry-link set.

Before migration implementation, tests must construct:
- an existing v1 Map with multiple revisions and branches;
- existing membership and placement payloads with no geometry manifests;
- verified and unverified Core Assets;
- cross-Project and cross-Map negative cases.

The migration must preserve all existing revision/cursor rows and remain atomic. No production backfill or compatibility dual-write is needed. Any active developer/test fixtures that represent formal geometry must be inspected and either mapped deterministically or explicitly recreated as test fixtures; do not delete Asset bytes or test fixtures merely because they are currently unreferenced by the new table.

## 6. Required verification

- POI / River / Landmass accept only their required roles and descriptor families.
- Missing/extra/duplicate component manifests and links are rejected.
- Component ID, role, Asset ID or hash mismatch is rejected.
- Unverified, missing, corrupt, wrong-Project and missing-byte Assets are rejected.
- Wrong Map Spatial Reference and absent/unaccepted precision profile are rejected.
- Object insertion with geometry membership is all-or-nothing across identity, reservation, registration, definition, revision, member, link and cursor.
- Revision append is all-or-nothing across revision, complete members, complete links and cursor.
- Injected failure at every formal DB write stage leaves no partial formal state; retry with the same logical component identity is deterministic.
- Editing geometry publishes new bytes; old revisions retain old Asset links and bytes.
- Undo/Redo and branch navigation preserve exact link sets after close/reopen.
- A component cannot be reintroduced after removal by reusing its identity.
- Fingerprint v1 fixtures remain byte-for-byte valid; v2 is deterministic and order-independent; mixed v1/v2 lineage works.
- Asset reconciliation treats revision links as references and never removes referenced bytes.
- Windows/macOS Debug and Release CI pass.

## 7. Implementation sequence

1. Extend Domain records, manifest parsing and fingerprint version dispatch.
2. Add EF entities/model configuration/migration for revision-scoped links.
3. Extend append and Object-insertion plans/transactions to derive, validate and atomically persist complete links.
4. Add Application orchestration for byte publication, Core Asset registration/verification and Domain commit.
5. Extend Asset integrity/orphan reconciliation to include revision-scoped links.
6. Add real-SQLite migration, transaction failure-injection, branch/reopen and cross-owner tests.
7. Independently review code, migration, tests and Windows/macOS CI before merge.

## 8. Explicitly deferred

- Numeric precision profile definitions, quantization, tolerance and valid extent (DESIGN-EXTENT / PRA-03).
- Generic component registry/table.
- Building/Cave Definition migration into Map Initial State.
- Post-zero Command geometry publication.
- Geometry split/merge, topology editing, hydrology, elevation and terrain fields.
- Renderer/SceneItem projection and viewport interaction.
- PRA-05 batching, chunking, LOD and performance optimization.

## 9. Review checklist

The independent review must specifically challenge:

- whether a per-component link row is the correct relational projection for revision history;
- composite FK and unique-index correctness;
- mixed fingerprint v1/v2 semantics and immutability;
- component identity no-reuse validation across revision branches;
- atomic Object insertion when required geometry is present;
- Asset orphan/reconciliation safety;
- the precision-profile fail-closed boundary.

No implementation is authorized to weaken the authoritative FOUNDATION-01 contract or to turn Workspace Presentation state into Core Domain revision content.
