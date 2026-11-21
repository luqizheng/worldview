# World View Core Domain — P2-09-06 Asset Store and Checkpoint Persistence Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — recommended asset and Checkpoint lifecycle; payload formats and exact lineage mechanism remain open
- Depends on: P2-09-01/02; P2-09-03; P2-09-05; D01-65~69 and D01-101~143; P2-08 V0.2.

## 1. Purpose

Define the local file persistence boundary for large formal map assets, AI staging assets and Core Domain Checkpoints. Keep file bytes immutable, semantic ownership explicit, and recovery safe across SQLite and filesystem failure boundaries.

## 2. Asset Store architecture

Recommended project-local structure:

- One project-local Asset Store managed by application persistence infrastructure.
- Immutable payload files stored by content identity (SHA-256 recommended).
- SQLite stores metadata, integrity state and semantic references.
- Core Domain owns formal asset references used by Map, Initial State, Commands, Objects and Checkpoints.
- AI Assistant owns its staging asset references until a promotion operation is accepted by Core Domain.

The Asset Store is infrastructure, not a third semantic authority. It does not decide what an asset means or which Map/Branch owns it.

## 3. Asset categories and ownership

| Asset category | Semantic owner | Lifecycle |
|---|---|---|
| Formal geometry payload | Core Domain | Published immutable asset referenced by formal domain data |
| Terrain/raster payload | Core Domain | Published immutable asset referenced by Initial State or formal Commands |
| Attribute/relationship payload too large for inline storage | Core Domain | Published immutable asset referenced by its owning domain record |
| AI-generated geometry/attribute staging | AI Assistant | Temporary/staged; tied to Candidate/Task lifecycle |
| Candidate preview/render artifacts | AI Assistant or application preview service | Disposable derived artifacts; never formal World State |
| Checkpoint payload | Core Domain | Immutable derived cache asset; disposable/rebuildable |

Small structured values may remain inline in SQLite where practical. Do not force every attribute or relationship into a separate file. Use files for large geometry, raster, binary or otherwise substantial payloads.

## 4. Immutable asset identity and metadata

Conceptual asset metadata should establish:
- stable asset identity or content hash;
- payload category/media type;
- payload format and format version;
- cryptographic content hash (SHA-256 recommended);
- byte length;
- project-relative locator;
- publication/integrity status;
- semantic owner/reference links;
- creation transaction metadata and retention/reference state.

Rules:

1. Published bytes are immutable; never overwrite a published asset in place.
2. A changed payload creates a new asset identity/content hash.
3. A hash identifies bytes, not semantic ownership.
4. Multiple domain records may reference the same immutable bytes when their content is identical and policy permits reuse.
5. A project-relative locator must remain valid when the whole project directory is moved.
6. Absolute machine-specific paths must not be persisted as formal asset references.
7. AI staging files must not be visible as formal Core Domain assets until promoted through Core Domain services.

## 5. Asset publication protocol

SQLite and filesystem writes do not share a native atomic transaction. The design must tolerate orphan files and must not claim cross-resource atomicity.

Recommended publication order:

1. Write the new payload to a temporary file under the target project Asset Store filesystem.
2. Validate payload structure and format version.
3. Compute cryptographic hash and byte size.
4. Flush file data/metadata according to the selected platform durability policy.
5. Publish to the immutable content-addressed location using an atomic filesystem operation where supported.
6. In a short SQLite transaction, create/confirm the asset metadata and the owning domain reference.
7. Commit the database reference.

Failure behavior:
- If the file write/publish fails, no formal database reference is committed.
- If SQLite rolls back after file publication, the file is an orphan candidate, not a formal asset.
- Orphan cleanup must recheck committed references and active publication records before deletion.
- If a hash-named file already exists, verify its size/hash before reuse; do not assume filename alone proves integrity.
- Never delete a file merely because one transaction failed; it may already be referenced by another committed record.

## 6. AI staging to formal asset promotion

- AI Assistant owns temporary Candidate assets and their metadata.
- Candidate assets remain outside the Core Domain Checkpoint namespace.
- Before formal submission, the exact asset bytes are made durable and hash-verified.
- Promotion is performed through a Core Domain service that validates the payload and creates a formal semantic reference.
- The authorized proposal manifest binds the exact asset content hash and relevant format/version metadata.
- A post-authorization asset change invalidates the authorization and requires a new proposal version and human authorization.
- A failed Task may clean up unreferenced staging assets according to AI lifecycle rules; it must not delete any asset already referenced by committed Core Domain data.

## 7. Checkpoint role and identity

Checkpoint is a derived acceleration artifact for historical reconstruction.

It is not:
- formal History;
- a Command or State Change;
- a World State Version or Object Version domain entity;
- a replacement for Initial State or authoritative history;
- an AI Candidate or staging result.

A Checkpoint is associated with:
- one selected Map/Branch;
- one Historical Time;
- one complete applicable inheritance-path context;
- one immutable payload and format version;
- integrity and publication metadata;
- reference/lifecycle state needed to prevent eviction while in use.

For a given Branch and time point, there is at most one effective current Checkpoint. Old invalid payload bytes may remain temporarily while referenced, but they are not logically usable.

## 8. Checkpoint lineage and reuse

- Checkpoint validity is determined by Core Domain using the complete inherited-path context, not merely the leaf Branch identity.
- The context includes every relevant ancestor segment up to each fork boundary and the Branch's own applicable history segment.
- A descendant may reuse an ancestor Checkpoint when that Checkpoint represents the exact inherited state at the relevant boundary and the full lineage remains valid.
- Reuse does not change the Checkpoint's owning Branch or make it a new authoritative record.
- The exact lineage fingerprint/version-combination algorithm is Core Domain-owned.
- Do not use AI-owned Branch Revision counters, Object Version entities, timestamps alone, or Checkpoint file hashes as substitutes for complete lineage validation.

## 9. Checkpoint publication protocol

Recommended lifecycle:

1. Core Domain selects the target Branch/time and captures the complete lineage context.
2. Build payload in a temporary location.
3. Validate payload completeness and internal format.
4. Compute cryptographic hash and size.
5. Publish immutable payload bytes to the project Asset Store.
6. Revalidate the complete lineage/context with Core Domain immediately before publication.
7. In one short SQLite transaction, persist Checkpoint metadata, mark it complete/current and bind the immutable payload reference.
8. Make it available to readers only after the complete metadata commit.

If the lineage changed while the Checkpoint was being built, discard publication rights. The output may be retained as an unreferenced orphan candidate and later cleaned up, but it must never be marked usable for the stale context.

A `complete` marker is required at the logical level. It must mean that payload validation, hash verification, final lineage validation and metadata publication all succeeded. An incomplete Checkpoint is never usable.

## 10. Invalidation, rebuild and eviction

### 10.1 Logical invalidation

- Ancestor history edits invalidate every affected descendant Checkpoint whose inherited path includes the changed ancestor segment.
- Changes outside a descendant's inherited boundary do not invalidate that descendant's Checkpoints.
- A Branch's own history edits invalidate affected Checkpoints on that Branch and relevant descendants.
- Invalidation is logical first; it does not require immediate physical deletion.
- Inactive Branches may remain stale and rebuild on demand when selected.
- The active path may be rebuilt immediately according to Core Domain policy.

### 10.2 Publication race

If an ancestor changes while a rebuild is running:
- the in-progress rebuild loses publication rights;
- final lineage validation fails;
- its output must not be marked complete/current;
- the affected path is rebuilt using the new context.

### 10.3 Physical eviction

- A logically invalid Checkpoint may remain on disk while an active reader/reference still uses it.
- Referenced Checkpoint files cannot be physically evicted until references are released.
- Logical invalidation and physical file deletion are separate operations.
- Cleanup must be conservative and verify both metadata references and active-use references.
- Do not provide manual deletion of an individual Checkpoint as a way to repair history; a full Checkpoint regeneration operation may be exposed under Core Domain policy.

## 11. Corruption and missing payload behavior

- Verify payload integrity before use according to the stored hash/format metadata.
- A missing or corrupt Checkpoint is a cache failure, not formal history loss.
- Reconstruct from authoritative Initial State + applicable Commands/State Changes.
- Corruption of one Checkpoint does not logically invalidate other Checkpoints.
- Corruption does not propagate through Branch inheritance.
- The active Branch's corrupt Checkpoint may be rebuilt automatically; an inactive Branch's corrupt Checkpoint is handled when that Branch is selected.
- Never mark a corrupt or incomplete payload as complete/current.

Legacy D01 references to `md5.txt` may be recognized for compatibility/audit, but MD5 is not recommended as the new content identity or security-grade integrity mechanism. SHA-256 is the recommended default unless a verified compatibility requirement dictates otherwise.

## 12. Backup, restore and project portability

The SQLite database and Asset Store form one Project consistency unit.

- Backup must capture a mutually consistent database and all referenced immutable assets.
- A backup must not omit a referenced asset merely because it is a cache; formal assets and Checkpoints have different recovery policies but both need coherent metadata handling.
- Restore must verify asset presence and integrity before exposing the Project as fully healthy.
- Missing/corrupt Checkpoints may be rebuilt from formal history.
- Missing/corrupt formal assets are data-integrity failures and must be reported; do not silently substitute a Checkpoint or AI Candidate.
- Project relocation must preserve project-relative references.
- Orphan cleanup should run only after restore/reconciliation has established the authoritative reference set.

A future backup format may optimize by omitting rebuildable Checkpoint payloads and regenerating them, but this must be an explicit backup policy and must preserve all authoritative assets. It is not assumed here.

## 13. Minimum verification scenarios

1. Two identical payloads can reuse one immutable content-addressed file when allowed.
2. Different payload bytes never overwrite an existing published asset.
3. A published file is hash-verified before formal reference creation.
4. A failed DB transaction after file publication leaves an orphan candidate, not a formal asset.
5. Orphan cleanup never deletes a referenced asset.
6. A changed AI Candidate asset after authorization invalidates the proposal authorization.
7. AI staging assets cannot be referenced as formal assets without promotion.
8. A Checkpoint is not usable before its complete marker is committed.
9. A Checkpoint cannot publish if lineage changes during build.
10. An ancestor Checkpoint can be reused by a descendant only when full inherited-path validation succeeds.
11. An ancestor edit invalidates only descendants whose inherited boundary includes that edit.
12. An edit after a descendant fork boundary does not invalidate that descendant's inherited state.
13. A referenced Checkpoint cannot be evicted while in use.
14. A corrupt Checkpoint is rebuilt without changing formal history.
15. Corruption of one Checkpoint does not invalidate unrelated Checkpoints.
16. Active and inactive Branches follow their respective rebuild policies.
17. Database backup/restore and Asset Store restore produce a consistent Project.
18. Missing formal assets are reported and never silently replaced by Checkpoints or Candidate data.

## 14. Open items before physical DDL

1. Exact geometry/raster/attribute/relationship payload formats and format versioning.
2. Exact project-relative Asset Store directory layout and atomic publish behavior on Windows/macOS filesystems.
3. Exact Checkpoint serialization format and compression strategy.
4. Complete lineage fingerprint/version-combination algorithm.
5. Exact active-reference tracking and safe eviction implementation.
6. Whether backup defaults include Checkpoint payloads or regenerate them after restore.
7. Platform-specific durability guarantees and flush/rename behavior.

D01-77's precise Checkpoint restoration strategy remains unresolved in the source discussion and is intentionally not decided here. This contract does not invent a recovery order beyond the verified lineage/rebuild rules above.

## 15. Status

This document establishes the recommended boundary: **immutable file-backed assets with domain-owned semantic references; AI staging kept separate; Checkpoints are immutable, lineage-validated, atomically published and rebuildable caches.**

It is not a final payload format, lineage algorithm, backup implementation or executable schema. No implementation authorization is implied.