# World View P2-09-16 — Shared Object Current-Definition and Checkpoint Coherence Contract V0.1

- Date: 2026-11-21
- Status: Recommended persistence/reconstruction contract — applies the user-confirmed retroactive Shared Object definition rule; not executable DDL or implementation authorization
- Depends on: ADR-003 Amendment V1.3; P2-08 Resolution V0.3; P2-09-06; P2-09-07 V0.2; P2-09-11 V0.8; P2-09-14

## 1. Purpose

Specify how a single current Project-level Shared Object definition participates in every historical reconstruction that references it, while preserving Map/Branch-local runtime histories and keeping Checkpoints safe when definitions change during or after reconstruction.

This contract does not add a Shared Object History, definition version chain, Object Version entity, per-Map definition copy, or AI-owned shadow state.

## 2. Controlling semantic rule

1. A Shared Object has one stable Project-level identity and one current authoritative definition.
2. All Maps referencing that identity resolve the same current definition.
3. A definition edit is project-level authoring outside the Map Historical Time axis. It is not a Command, Event, Event Effect, or State Change.
4. Reconstruction at every Historical Time resolves the current definition, including reconstruction of any valid Map Historical Time that precedes the edit's transaction timestamp.
5. V1 does not provide an as-of query for the definition as it existed at a previous authoring/transaction time.
6. Map Initial State, selected History inheritance, and post-zero Commands/Events/Effects remain Map/Branch-specific. Shared definition does not mean shared runtime state.
7. Event Effects targeting a Shared Object alter only runtime state on their owning selected Map/Branch History path; they never write the Project-level definition.

## 3. Definition versus runtime-state ownership

Every field/property used by reconstruction must have exactly one semantic owner:

- **Shared definition/base data:** intrinsic, Project-level facts that should be identical for all Maps referencing the Shared Object and whose edits intentionally affect all historical reconstructions.
- **Map Initial State:** Map-specific initial membership, placement/context, and initial values at time zero.
- **Map/Branch runtime state:** values changed by post-zero Commands or Event Effects on that selected History path.

A property must not be stored as an independently writable value in more than one of these layers. The Core Domain Object Type/descriptor catalog must classify each property and its allowed mutation operations. A command/effect handler must reject attempts to mutate a definition-owned field through runtime history; a definition editor must not overwrite a Map/Branch runtime value.

The classification is semantic, not inferred from the current JSON location or UI form. Moving a property between ownership classes requires an explicit schema/domain migration and compatibility review.

## 4. Reconstruction input and fingerprint

The Core Domain computes one deterministic reconstruction-input fingerprint for a selected Map/Branch and target Historical Time. The fingerprint covers at least:

1. selected Map identity and selected History inheritance path, including every fork boundary;
2. Map Initial State and its referenced immutable assets;
3. every Shared Object definition reachable from the Map's formal object references and applicable history inputs, including stable Object identity, definition schema version, canonical definition content, and referenced immutable asset content hashes;
4. applicable Commands, Events and Event Effects, their payload/schema versions and execution-semantics identifiers;
5. explicit ordering constraints and all other authoritative history inputs that affect execution;
6. required immutable asset identities/content hashes; and
7. the Core Domain reconstruction algorithm/semantics compatibility identifier where a change could alter results.

Canonicalization requirements:
- Use a stable, deterministic serialization for definition content; object-key ordering, numeric representation, Unicode normalization and null/omitted-field treatment must be fixed by the Core Domain serializer.
- Hash canonical bytes with a modern cryptographic hash (recommended SHA-256 or stronger supported equivalent).
- Include stable Object identity and schema/semantics identifiers so equal payload text belonging to different objects or incompatible schemas cannot collide semantically.
- Exclude `created_at`, `updated_at`, audit text, UI presentation metadata and other transaction-only fields that do not affect reconstruction.
- Do not use a mutable `definition_revision`, `object_version`, or historical version table as a substitute for content identity.

The fingerprint is a derived cache-validation value, not an authoritative version number or historical record. The existing `core_checkpoint.reconstruction_input_fingerprint` stores the resulting aggregate fingerprint; no separate definition-history table is required.

## 5. Checkpoint invalidation and stale-publication protection

When a Shared Object definition changes:

1. Persist the new current definition through the Core Domain definition-authoring service in a short SQLite transaction.
2. Identify all Maps whose formal references or applicable history inputs can resolve that Shared Object.
3. Mark their affected reconstruction/Checkpoint contexts logically stale, including all descendant Branch paths rooted in those Maps. Do not physically delete checkpoint payloads in the same semantic operation; cache eviction is a separate safe-cleanup concern.
4. Prevent any reconstruction result computed from the old definition input from being published as current.
5. Rebuild the active selected path according to the normal reconstruction lifecycle; inactive paths may rebuild on demand when selected.
6. Preserve all Commands, Events, Event Effects and Map/Branch runtime histories. Do not synthesize replacement timeline records.

In-flight publication protocol:
- At reconstruction start, capture the complete input fingerprint and build the derived result against that immutable input snapshot.
- Before publishing a Checkpoint, recompute or revalidate the complete current input fingerprint using Core Domain-owned logic.
- Publish the Checkpoint metadata as `complete` only if the final fingerprint exactly matches the captured fingerprint and the payload integrity checks pass.
- The final revalidation and transition to `complete` must be protected by a short transaction/serialization boundary so a definition edit cannot commit between the successful recheck and the Checkpoint publication.
- If the fingerprint differs, discard the result as stale (or retain it only as an unpublished temporary artifact for cleanup); it must not be selectable by reconstruction/playback.
- If a definition is edited and later restored byte-for-byte to the same canonical content with the same schema/semantics, a result built from that exact same reconstruction input may be valid. Transaction timestamps alone do not make semantically identical inputs different.

No long-running reconstruction, geometry processing or file I/O may be performed while holding the final SQLite write transaction. Immutable Checkpoint payload publication follows the existing Asset/Checkpoint filesystem protocol; the short database transaction only performs final context revalidation and metadata publication.

## 6. Affected-scope rules

| Change | Affected reconstruction scope | Must not affect |
|---|---|---|
| Shared Object current-definition edit | Every Map that references/resolves the Shared Object, and descendant Branch paths | Runtime histories of other Maps; their Event execution; unrelated Maps |
| Map Initial State edit | That Map and all Branches rooted in it | Other Maps' baselines or shared identity definition |
| Map-local Command/Event/Effect edit | Owning Map History path and descendants that inherit the changed item | Other Maps merely referencing the same Shared Object |
| Branch-local Command/Event/Effect edit | That Branch path and descendants inheriting it | Parent, sibling or unrelated Branch paths |
| Narration-only edit | Narration presentation/index state | World State fingerprint and World State Checkpoints |
| Calendar display configuration edit | Date interpretation/display for affected intervals | Internal Historical Time, Command/Effect order, World State fingerprint and World State Checkpoints |

A Map-level reference is distinct from time-zero membership. A Shared Object absent from one Map's Initial State may still be a valid referenced identity for a later history operation; the reference/reachability rules determine whether it belongs in that Map's reconstruction dependency set. The implementation must not equate `core_map_object_ref` with initial membership.

## 7. Required acceptance tests

1. Two Maps reference one Shared Object. Editing its definition changes reconstruction output in both Maps at past and present Historical Times.
2. A historical Checkpoint built before the edit is rejected after the edit, even when its target time predates the edit.
3. A Map that does not reference the Shared Object remains eligible to use its existing Checkpoints.
4. A Map reference without time-zero membership is handled according to formal reachability rules and does not silently create initial membership.
5. A Map A Event Effect changes the Shared Object runtime state only in Map A; Map B retains its own runtime state while both resolve the same current base definition.
6. A Shared Object definition edit does not create a Command, Event, Event Effect, or State Change.
7. An Event Effect cannot mutate a definition-owned property; a definition edit cannot overwrite a Map/Branch runtime-owned property.
8. A rebuild started before a definition edit cannot publish its stale Checkpoint after the edit commits.
9. A rebuild whose input fingerprint is unchanged may publish successfully even if unrelated audit metadata changes.
10. If the definition is changed and then restored to identical canonical content before publication, final fingerprint equality permits publication only when all other inputs also match.
11. A failed reconstruction after a definition edit preserves the new authoritative definition and reports the affected Map/Branch history as failed/stale under the existing Fail-Fast repair lifecycle; it does not silently roll back the definition.
12. Narration-only and Calendar-display-only edits do not invalidate World State Checkpoints.

## 8. Physical schema implications

- Keep one current Shared Object definition row/payload per Shared Object identity.
- Do not add `shared_object_definition_version`, `object_version`, `shared_object_history`, or per-Map definition-copy tables.
- Keep `core_checkpoint.reconstruction_input_fingerprint` as the aggregate validation field. A separate persisted per-definition fingerprint column is optional and should not be added unless a measured query/diagnostic need requires it.
- Existing typed asset links for Shared Object definitions must resolve immutable content-addressed assets. A changed asset produces a new immutable Asset record/reference; it never overwrites old bytes.
- Definition authoring and affected-cache invalidation metadata must be committed consistently. Cache rows may remain physically present but are logically unusable when their stored input fingerprint no longer matches.
- The application may maintain a rebuildable in-memory reverse dependency index. If a persistent reverse lookup index is needed, use the existing Map/Object reference relation and indexes; do not create a new authoritative dependency graph.

## 9. Status

This contract operationalizes the already-confirmed retroactive Shared Object definition rule. It is a recommended Core Domain persistence/reconstruction contract and a prerequisite for finalizing Checkpoint publication logic. It does not authorize implementation, assert provider proof, or introduce historical definition versioning.