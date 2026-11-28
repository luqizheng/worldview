# FOUNDATION-01C — Geometry Asset Publication Design v0.1

- Date: 2026-11-28 (amended 2026-11-29 per independent review; see §10)
- Status: DESIGN UNDER INDEPENDENT REVIEW; AMENDED PER 01C-REVIEW-01…08
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

**Required-roles-only enforcement (01C-REVIEW-01).** The embedded descriptor catalog declares more geometry roles than the accepted first slice — e.g. the `river` descriptor declares both `centerline` (min 1) and `reach` (min 0, same families). Per ARCH-01 decision 1, only the required role of each accepted type is in scope. 01C must therefore validate manifests against a **required-roles-only projection** of the descriptor (exactly `{location}`, `{centerline}`, `{extent}` above), not against the raw descriptor role list. A manifest whose `geometry_role` is declared by the descriptor but is not the accepted required role (counter-example: a `river` `reach` component) must be rejected with a typed conflict, and a negative test must prove it. Reusing `GeometryRules.ValidateManifest`/`ValidateManifestCollection` is allowed only behind this projection; calling them with the raw descriptor is an implementation defect.

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

The link is per component, not per distinct Asset. Two components may reference the same immutable Asset and therefore have two component-specific rows (01C-REVIEW-08: sharing one immutable Asset across components is explicitly legal). This preserves the complete component-to-Asset mapping and avoids losing component identity in a generic owner/Asset pair. The manifest remains the semantic authority; the relational row is its typed integrity projection.

**Component identity scope (01C-REVIEW-08).** Component identity is scoped to its owning Object (P2-09-19 §6.2); the unique key above intentionally permits the same opaque GUID under two different Objects of one revision. "Never recycled" applies per owning Object: once an Object's component has appeared in any committed revision of the baseline, that Object must never re-issue the same GUID for different geometry, nor re-introduce a removed component ID with new content.

Do not add `core_geometry_component`, a generic polymorphic owner table, or a second Object registry.

### 3.3 Exact-set validation

The Domain derives the complete typed-link set from the canonical membership payloads. Callers do not supply a partial or independently mutable link list.

Before commit, the adapter re-reads and validates within the same SQLite transaction:

1. Map, baseline, expected active revision and cursor version;
2. every member's Object identity, active lifecycle, Map registration and pinned descriptor;
3. every manifest's unique component identity, the accepted **required role only** (per the §1 required-roles-only projection; any other declared role is rejected — 01C-REVIEW-01), permitted family, 2D dimension and schema version;
4. manifest Spatial Reference equals the Map's immutable bound Spatial Reference, and the payload precision-profile reference passes the §4 gate;
5. every referenced Asset belongs to the same Project, is `Verified`, and has the exact manifest content hash;
6. every component maps to exactly one typed link row and no extra link exists;
7. component identity is retained for an existing component and is not reused after removal — enforced inside the transaction by resolving, for each member Object, the union of that Object's Geometry Component IDs across **all committed revisions of the same baseline** (including abandoned branches, parsed from the canonical membership payloads): a candidate manifest component ID that is absent from the child snapshot but present in any earlier revision of the same Object is rejected, and a candidate that re-points an already-used component ID to different geometry (different role/family/hash) is rejected (01C-REVIEW-03). This is an in-transaction scan of an already-bounded set (revisions of one baseline of one Map, single-machine scale); no registry table is introduced;
8. the complete child member snapshot, complete typed-link set, revision metadata and cursor move are committed together.

The persistence adapter must re-derive the expected links from the exact canonical member rows inside the transaction; it must not trust a Domain pre-read or a caller-provided link list.

**Read-path consistency (01C-REVIEW-04).** Relational constraints can prove that a link row belongs to a committed revision member and a same-Project Asset; they cannot prove that the row matches the manifest content (a hand-written SQL `INSERT` can create a link whose component ID exists in the member row's registration context but not in that revision's manifest). The design therefore requires the same fail-closed read contract as 01B fingerprints: whenever the Domain resolves a revision snapshot or the active state for a revision whose composition carries geometry manifests, it re-derives the expected link set from the manifests and compares it with the stored link rows; any difference is a `CoreDomainDataException` (data corruption, never silently repaired). Write-path exact-set validation plus read-path re-projection together pin the manifest↔link↔Asset invariant; the database enforces relational existence, Core Domain enforces content agreement (same division of labor as the 01B immutability triggers).

### 3.4 Object insertion is part of the same gate

The current `InsertObject` path can append an Initial State membership revision atomically, but it has no geometry Asset references. For a descriptor whose required geometry role has minimum cardinality greater than zero, Object insertion must accept a complete geometry-bearing Initial Membership and publish its revision-member row plus typed Asset links in the same existing Object insertion transaction.

It must not create an active registered Object with a formally incomplete required geometry membership and rely on a later repair transaction. **First-slice authoring is one-step (01C-REVIEW-05).** For `point_of_interest`, `river` and `landmass`, the accepted authoring path is Object insertion with a complete required-geometry Initial Membership in the same transaction; a two-step flow (insert a membership-less active Object of these types first, then append the geometry-bearing membership) is not an accepted authoring path for the first slice and must not be offered by first-slice Application use cases. The existing no-membership insertion semantics remain available unchanged for the other accepted types and non-authoring use cases; an active, registered POI/River/Landmass object without an Initial State membership can therefore only originate from those pre-existing use cases, is not produced by first-slice authoring, and enters the time-zero baseline only through a later membership append that satisfies the full manifest gate.

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

**Composition details (01C-REVIEW-06).** For a member whose payload contains no `geometry_components` manifest (non-first-slice types) the v2 composition hashes the same membership/placement content with an empty manifest section and an empty link projection — deterministically, order-independently, and without interpreting payload-external state. For revision zero (empty snapshot) the v2 composition is the empty-manifest/empty-projection case; its constant (the v2 analogue of 01B's `EmptySnapshotFingerprint`) is computed once by Domain and reused by Map creation and any migration, so Map creation's fingerprint-vs-plan re-derivation keeps working unchanged. The version dispatch is a function of the revision row's stored composition version — read validation, append recomputation, and Map creation each select v1 or v2 explicitly from stored data, never from global configuration.

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

**Gate rule (01C-REVIEW-02).** The precision-profile reference of a geometry payload is the `coordinate_precision_profile_id` field that already exists in the `worldview.geometry-json` Asset envelope (GeometryAssetEnvelope currently checks only its presence and type). The publication gate is defined as follows, evaluated inside the SQLite transaction as part of §3.3 validation:

1. The Map's bound Spatial Reference **must** carry a precision profile (`PrecisionProfileId` non-null). If it is null — the state of every Map created on current master — geometry publication is rejected with a typed conflict (`spatial-reference precision policy is not defined; geometry publication is unavailable until DESIGN-EXTENT/PRA-03 establishes a precision profile`), exactly mirroring the Field Revision gate in `SpatialReferenceRules`.
2. The envelope's `coordinate_precision_profile_id` must equal that Spatial Reference's `PrecisionProfileId` exactly (ordinal). The comparison happens where the envelope text is validated — Application-side byte validation before the transaction (§3.7 step 1), against the Spatial Reference record. Because the Spatial Reference definition is immutable (FOUNDATION-01A), the record read outside the transaction cannot drift from the one re-read inside it; the in-transaction validation still independently enforces rules 1 (profile existence) and the manifest↔SR equality of §3.3 item 4, so a mismatched envelope or an absent profile cannot pass at any stage.
3. No default, fallback, or placeholder profile may be synthesized by the publication path; the gate is purely equality against the stored SR policy.

Consequence on the current baseline: because no production precision profile exists yet, **every** formal geometry publication is rejected until DESIGN-EXTENT adopts one. 01C implements the typed infrastructure and tests it with explicit test-policy fixtures that install a profile only inside the test's own database (never through a production code path, never into shared fixtures used as production defaults). No numeric values, quantization rules, tolerances, or valid extents are introduced by 01C.

## 5. Migration and development-data handling

The schema migration adds the revision-scoped typed link table and indexes/constraints. It does not rewrite immutable v1 revisions, their members, fingerprints or operation payloads. Existing revisions have an empty geometry-link set.

**Reconciliation reference semantics (01C-REVIEW-07).** `FindOrphanAssetIds` currently excludes only `core_object_definition_asset` references (SqliteCoreDomainDatabase.Assets.cs). The 01C extension must treat a Core Asset as referenced if **any** committed revision of the Project carries a link row for it — including revisions that the active cursor does not point at, abandoned branches, and revisions of closed Maps. An Asset referenced only by historical revisions is never a cleanup candidate. Conversely, a Core Asset metadata row with zero Definition links and zero revision links (the residue of a failed §3.7 flow between byte publication and DB commit, or between `RegisterAsset` and the append) remains an orphan that only safe reconciliation may address, and its immutable bytes follow the same rule.

Before migration implementation, tests must construct:
- an existing v1 Map with multiple revisions and branches;
- existing membership and placement payloads with no geometry manifests;
- verified and unverified Core Assets;
- cross-Project and cross-Map negative cases.

The migration must preserve all existing revision/cursor rows and remain atomic. No production backfill or compatibility dual-write is needed. Any active developer/test fixtures that represent formal geometry must be inspected and either mapped deterministically or explicitly recreated as test fixtures; do not delete Asset bytes or test fixtures merely because they are currently unreferenced by the new table.

## 6. Required verification

- POI / River / Landmass accept only their required roles and descriptor families; a declared-but-not-required role component (counter-example: `river` `reach`) is rejected (01C-REVIEW-01).
- Missing/extra/duplicate component manifests and links are rejected.
- Component ID, role, Asset ID or hash mismatch is rejected.
- Unverified, missing, corrupt, wrong-Project and missing-byte Assets are rejected.
- Wrong Map Spatial Reference is rejected; absent Map precision profile rejects every formal publication, and an envelope `coordinate_precision_profile_id` that differs from the Spatial Reference's profile is rejected (01C-REVIEW-02).
- Resolving a snapshot or the active state whose stored links disagree with the manifests fails closed with `CoreDomainDataException` (01C-REVIEW-04).
- Object insertion with geometry membership is all-or-nothing across identity, reservation, registration, definition, revision, member, link and cursor.
- Revision append is all-or-nothing across revision, complete members, complete links and cursor.
- Injected failure at every formal DB write stage leaves no partial formal state; retry with the same logical component identity is deterministic.
- Editing geometry publishes new bytes; old revisions retain old Asset links and bytes.
- Undo/Redo and branch navigation preserve exact link sets after close/reopen.
- A component cannot be reintroduced after removal by reusing its identity, and an in-use component ID cannot be re-pointed to different geometry within the same Object (01C-REVIEW-03).
- Fingerprint v1 fixtures remain byte-for-byte valid; v2 is deterministic and order-independent; manifest-less members and revision zero have deterministic v2 compositions; mixed v1/v2 lineage works (01C-REVIEW-06).
- Asset reconciliation treats **all** historical revision links as references and never removes referenced bytes; a metadata row with zero Definition and zero revision links remains an orphan (01C-REVIEW-07).
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

## 10. Independent review amendments (2026-11-29)

The independent design review of this document (baseline `e24b071174df666dbfd75cb2d97653bb59723c63`, PR #55) confirmed the model direction and raised the following findings. Items 01–05 are **required** before implementation; items 06–08 are recorded clarifications incorporated above. All amendments are documentation-only; no production code or migration is authorized by this review.

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| 01C-REVIEW-01 | P1 | The embedded `river` descriptor declares a second geometry role `reach` (min 0) alongside the accepted `centerline`; `GeometryRules.ValidateManifest`/`ValidateManifestCollection` validate against the raw declared-role list, so a `reach` component would pass validation and leak a role that ARCH-01 decision 1 keeps out of scope. | §1 now mandates a required-roles-only descriptor projection with a typed rejection and a negative test. |
| 01C-REVIEW-02 | P1 | §4 required a precision gate but did not define where the payload's precision-profile reference lives or how "accepted" is evaluated; `GeometryComponentManifest` has no precision field and `GeometryAssetEnvelope` only checks presence of `coordinate_precision_profile_id`. | §4 now defines the exact gate: SR `PrecisionProfileId` must be non-null and the envelope's `coordinate_precision_profile_id` must equal it (ordinal), mirroring the Field Revision gate; no placeholder profiles. On the current baseline every formal publication is therefore rejected until DESIGN-EXTENT. |
| 01C-REVIEW-03 | P2 | Component-identity no-reuse was required (§3.3 item 7, §6) with no enforcement mechanism; `GeometryRules` defers it to the "persistent owner service", which does not exist on the Initial State path. | §3.3 item 7 now specifies the in-transaction union scan over all committed revisions of the same baseline for each member Object, with the rejection rules and the single-machine performance bound stated. |
| 01C-REVIEW-04 | P2 | Manifest↔link agreement was guaranteed on the write path only; hand-written SQL can insert a link row whose component does not exist in the revision's manifest, and no read-path check was defined. | §3.3 read-path consistency paragraph now requires fail-closed re-projection on snapshot/active-state resolution (`CoreDomainDataException`), matching the 01B read-side fingerprint contract. |
| 01C-REVIEW-05 | P2 | §3.4 left the two-step path (membership-less insert, then geometry append) ambiguous for the first-slice types. | §3.4 now states first-slice authoring is one-step; the two-step flow is not an accepted authoring path for POI/River/Landmass, while pre-existing no-membership semantics remain unchanged for other types. |
| 01C-REVIEW-06 | P3 | v2 composition details for manifest-less members and revision zero, and the v2 empty-snapshot constant scheduling for Map creation, were unspecified. | §3.6 composition-details paragraph added (empty manifest/projection encoding; v2 constant analogous to 01B `EmptySnapshotFingerprint`; dispatch from stored revision data only). |
| 01C-REVIEW-07 | P3 | Orphan reconciliation wording did not state that **all** historical revision links (abandoned branches, closed Maps) count as references; `FindOrphanAssetIds` today covers only Definition links. | §5 reconciliation paragraph added. |
| 01C-REVIEW-08 | P3 | Component-identity scope (per owning Object) and the legality of sharing one immutable Asset across components were implicit. | §3.2 scope paragraph added. |

Findings that were checked and need no change: per-component (not per-Asset) link rows; composite FK chain via the revision alternate key and member primary key (a link cannot exist without its exact revision member, and cross-Project Asset references are impossible); mixed v1/v2 lineage under Undo/Redo (cursor-only transitions, diff payloads carry no versioned content); fingerprint immutability of committed v1 rows (01B triggers + §3.6 prohibition); atomic Map-creation/InsertObject integration; byte-publication-then-short-transaction flow with orphan reconciliation on the current single-machine model; the Building/Cave Definition owner path remaining separate from the Map Initial State revision path.
