# World View Core Domain — P2-08 Shared Object / Map-local Event Semantics Resolution

- Version: 0.3
- Date: 2026-11-21
- Status: **Semantic resolution accepted; ADR-003 Amendment V1.3 is the controlling amendment; physical persistence remains open**
- Supersedes: V0.2

## 1. Executive resolution

The Shared Object model distinguishes shared definition/base data from Map/Branch runtime state.

1. A Shared Object has one stable identity and one current Project-level definition/base-data source.
2. Editing that definition changes the common data for every Map referencing the object.
3. Shared-definition edits are project-level authoring outside Historical Time; they are not post-zero Events or timeline Commands.
4. **Historical reconstruction at any Historical Time uses the current shared definition.** A definition edit is therefore retroactive with respect to the shared-definition input used to reconstruct earlier world-time points.
5. V1 does not retain replayable historical Shared Object definition versions or promise reconstruction of an earlier definition as-of a past authoring time.
6. Time zero is the first point of a Map timeline. The Map's Initial State is its authoritative starting input and contains Map-specific membership/placement/context while referencing, not copying, shared definitions.
7. Post-zero Event-driven runtime changes belong to the currently active Map/Branch's selected History path. Other Maps are inactive/silent and do not execute those Events.
8. The same Shared Object identity/current definition may have different runtime states in different Maps/Branches at the same displayed Historical Time.
9. A selected Map/Branch is reconstructed through one History inheritance path. No second independently replayed Shared Object History stream is composed with it.

## 2. Semantic categories

### 2.1 Shared Object definition/base data

- Project-level shared identity and current definition.
- Shared edits propagate to every referencing Map.
- Historical reconstruction at all world-time points resolves the current definition.
- Transaction-time audit may record edits, but it is not a replayable version chain.

### 2.2 Map time-zero Initial State

- One logical Initial State boundary per Map.
- Map-specific membership, placement/context and initial values are scoped to that Map.
- Shared Object references resolve to the current common definition.
- Branches inherit the root Map baseline; they do not copy it.

### 2.3 Post-zero Event-driven runtime change

- Belongs to the currently active Map/Branch History.
- Does not mutate shared definition/base data.
- Does not propagate its runtime effect to other inactive Maps.
- Is reconstructed only through the selected History inheritance path.

## 3. Reconstruction model

For a selected Map/Branch at Historical Time T, Core Domain resolves:

1. current definitions of the Shared Objects referenced by the Map;
2. the Map's authoritative time-zero Initial State;
3. the selected History inheritance path and accepted fork boundaries;
4. applicable post-zero Commands, Events and State Changes up to T, in deterministic order;
5. required immutable assets.

The result is one World State for that selected Map/Branch.

A shared-definition edit invalidates derived reconstruction artifacts and Checkpoints for every referencing Map and affected descendant Branches. In-flight reconstruction computed against an obsolete shared-definition input must not publish as current. A Map/Branch Event change invalidates only affected history paths and descendants inheriting the change.

## 4. Branch and deletion compatibility

No change is made to ADR-001/005:
- Branches inherit ancestor history at or before the closed fork boundary (`effective_time <= parent_at`).
- Branch-owned State Changes must satisfy `effective_time > parent_at`.
- Branches do not copy ancestor Commands/Events and do not merge.

ADR-003 A10-09 remains unchanged: a later property modification does not resurrect a deleted object; the command/history-quality warning semantics remain independent.

## 5. ADR-003 relationship

ADR-003 V1.2's independent Shared Object History interpretation is amended by ADR-003 Amendment V1.3. A10-04 through A10-09 and all unrelated frozen clauses remain unchanged. The amendment is the controlling semantic text for A10-01/A10-02 and the definition/runtime distinction.

## 6. AI Assistant boundary

AI Assistant must obtain mutation scope from Core Domain: Shared Object definition authoring versus Map/Branch runtime history. The exact scope must be included in proposal and authorization evidence. AI Assistant must not construct a composite history, maintain a shadow shared definition, or infer global propagation from ambiguous natural-language instructions.

## 7. Acceptance scenarios

1. Two Maps reference one Shared Object; a definition edit is visible in both.
2. Reconstructing either Map at a past Historical Time after the edit uses the current definition.
3. A post-zero Event in Map A does not execute in Map B.
4. The same Shared Object can have different runtime states in Maps A and B at the same Historical Time.
5. A Project-level Shared Object may be absent from a Map's Initial State.
6. Initial State membership edits affect only that Map and its descendant Branches.
7. Branches reuse the root Map Initial State and preserve accepted fork-boundary rules.
8. Shared-definition edits invalidate affected Checkpoints; stale in-flight reconstruction cannot publish.
9. Shared-definition edits do not create timeline Events or per-Map Commands.
10. A10-09 deletion-followed-by-modification semantics remain unchanged.

## 8. Status

The semantic conflict and historical-as-of behavior are resolved for V1. The accepted rule is current-definition retroactivity. Exact audit metadata, field classification, invalidation implementation, physical schema and transaction mechanics remain technical design items. This document is not executable DDL or implementation authorization.