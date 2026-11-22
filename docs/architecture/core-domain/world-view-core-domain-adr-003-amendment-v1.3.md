# World View — ADR-003 Amendment V1.3: Shared Definition and Map-local Runtime History

- Date: 2026-11-21
- Amendment status: **Accepted semantic amendment — preserve all unaffected V1.2 clauses**
- Supersedes: Only the conflicting Shared Object History interpretation in ADR-003 V1.2 §§1, A10-01, A10-02 and related explanatory wording
- Authority: User-confirmed semantics, including explicit confirmation that shared-definition edits are retroactive for historical reconstruction
- Scope: Shared Object definition/base data, Map time-zero Initial State, and post-zero Event-driven runtime changes

## 1. Amendment decision

This amendment distinguishes the shared definition of an Object from the runtime state of that Object in a selected Map/Branch.

1. A Shared Object has one stable identity and one current shared definition/base-data record. Every Map referencing that Shared Object uses that same definition.
2. Editing the Shared Object definition changes the common definition for every referencing Map.
3. Shared Object definition edits are project-level authoring changes outside the Map Historical Time axis. They are not post-zero Events and are not replayed as Commands on each Map/Branch History.
4. **Historical reconstruction at any Historical Time uses the current authoritative Shared Object definition.** A later shared-definition edit therefore also changes the shared base input used when reconstructing earlier points in every referencing Map.
5. V1 does not retain replayable historical Shared Object definition versions and does not promise reconstruction of the definition as it existed at an earlier authoring/transaction time.
6. Time zero is the first point of a Map timeline. The Map's Initial State supplies its Map-specific starting membership, placement/context and initial values; it references rather than duplicates Shared Object definition/base data.
7. After time zero, Event-driven runtime changes belong to the currently active Map/Branch's selected History path. Other Maps are inactive/silent and do not execute that Event. The same Shared Object identity can consequently have different runtime states in different Maps/Branches.
8. A selected Map/Branch is reconstructed through one History inheritance path. There is no second independently authoritative Shared Object History stream to compose with that path.

## 2. Replacement wording for ADR-003 A10-01

**A10-01 — Single selected-history reconstruction sequence**

For one selected Map or Branch, Core Domain reconstructs one state from the Map's time-zero Initial State, the current shared definitions referenced by that Map, and the applicable Commands/Events/State Changes on the selected History inheritance path, in deterministic order.

A10-01 does not mean that independent Shared Object and Map-local history streams are merged. Shared Object definitions are common current inputs; post-zero runtime evolution is represented on the selected Map/Branch History path. Playback remains single-selection and produces one state.

## 3. Replacement wording for ADR-003 A10-02

**A10-02 — Shared definition scope and Map/Branch runtime scope**

1. A change to a Shared Object's definition/base data changes that one shared definition and is visible to every Map referencing the Shared Object.
2. Such a definition edit is project-level authoring, outside Historical Time. It is not copied into each Map, represented as a per-Map Command, or replayed as an Event.
3. Historical reconstruction at any time uses the current shared definition. V1 does not preserve historical as-of shared-definition versions.
4. Map/Branch-local post-zero Event-driven runtime changes belong only to the currently active Map/Branch History path.
5. Other Maps do not execute the active Map's Events and are not changed by those runtime effects.
6. A Map's Initial State establishes its own time-zero membership and Map-specific initial context. Shared Object identity/definition remains common and is not duplicated as independently writable Map data.

## 4. Clarification of related clauses

- Any V1.2 wording that describes a Shared Object-owned, independently replayed Shared Object History is replaced by the definition-authoring model in this amendment.
- A10-03's Command-driven authoring principle remains applicable to Commands on a selected History; it does not turn shared-definition edits into timeline Commands.
- A10-04 through A10-09 remain unchanged, including explicit user ordering, warning lifecycle, single-Branch playback, and deletion-followed-by-modification semantics. Where A10-08's single-line rule is restated, it refers to one selected History path.
- ADR-001, ADR-002 and ADR-005 Branch inheritance rules remain unchanged. Ancestor history at `effective_time <= parent_at` is inherited; Branch-owned State Changes must satisfy `effective_time > parent_at`.
- No Merge/Rebase/Cherry-pick/Conflict Resolution model is introduced.

## 5. Required consistency and invalidation behavior

A Shared Object definition edit must invalidate derived reconstruction artifacts for every Map referencing that Shared Object and their affected descendant Branches. It does not append Events, rewrite Initial State, copy Commands, or synchronize runtime state between Maps.

The edit and its shared definition must be committed atomically by Core Domain. In-progress reconstruction must detect that its shared-definition input changed before publishing a result or Checkpoint; a result computed against an obsolete definition must not be published as current.

Map/Branch Event changes invalidate only the selected History paths and descendants that inherit those changes, under the accepted fork boundary.

## 6. Consequences and explicit non-goals

- V1 historical playback is a reconstruction of world-time history using today's shared definitions, not a full as-of reconstruction of all project authoring facts.
- Transaction-time audit may record who changed a shared definition and when, but that audit record is not a replayable definition version and does not change the rule in §1.4.
- No Shared Object revision chain, Shared Object History replay stream, Object Version, or per-Map copied definition is introduced.
- If exact historical-as-of shared definitions are required in a future release, that is a new product/domain decision and a separately versioned capability; it must not be smuggled into V1.

## 7. Acceptance scenarios

1. Maps A and B reference Shared Object S. Editing S's definition is visible in both.
2. Reconstructing Map A at a past Historical Time after the edit uses S's current definition.
3. Map A has a post-zero Event changing S's runtime state. Map B does not execute the Event and its runtime state remains unaffected.
4. At the same displayed Historical Time, Maps A and B may have different runtime states for S while sharing the same current definition.
5. A Branch inherits the root Map Initial State and accepted ancestor History path; it does not create a copied shared definition or a second history stream.
6. A shared-definition edit invalidates affected Checkpoints across referencing Maps/Branches.
7. A10-09 remains true: a later property modification does not resurrect a deleted object and retains the required history-quality warning.

## 8. Status

This is the accepted semantic amendment to ADR-003 V1.2. The original V1.2 text should be retained as historical source; this amendment is applied as the controlling replacement for the clauses identified above. It does not freeze physical schema, exact audit fields, transaction implementation, or Checkpoint fingerprint algorithms.