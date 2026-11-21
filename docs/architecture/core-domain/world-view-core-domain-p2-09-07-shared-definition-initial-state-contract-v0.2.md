# World View P2-09-07 — Shared Object Definition and Map Initial State Persistence Contract

- Version: 0.2
- Date: 2026-11-21
- Status: **Accepted semantic contract / design working baseline; physical schema remains open**
- Supersedes: V0.1
- Depends on: ADR-001, ADR-002, ADR-003 Amendment V1.3, ADR-005, P2-08 resolution V0.3

## 1. Purpose

Define the persistence boundary among Project-level Shared Object identity/definition, Map time-zero Initial State, and post-zero runtime history. This contract applies the user's explicit decision that Shared Object definition edits are retroactive for historical reconstruction: all historical reconstruction uses the current shared definition.

## 2. Shared Object identity and definition

- One Shared Object has one stable Project-level identity and one current authoritative definition/base-data record.
- Maps refer to that identity; they do not own independent writable copies of the definition.
- Editing the definition changes the common data for every Map referencing it.
- The edit is project-level authoring outside Historical Time.
- Historical reconstruction at any Historical Time resolves the current definition, regardless of when the reconstructed world-time point occurred.
- V1 does not persist replayable historical definition versions or promise old-definition as-of reconstruction.
- Transaction-time audit metadata may record the edit, but is not a historical playback stream or definition revision chain.

A definition field must be owned either by the Shared Object definition or by Map/Branch runtime state, never both. Core Domain descriptors and Domain Services enforce this classification.

## 3. Map time-zero Initial State

- Each Map has one logical Initial State boundary at the first point of its timeline (time zero).
- Initial State is Map-owned and is the authoritative starting input for reconstruction.
- It records Map-specific membership/availability, placement/context, initial values and required references.
- A Shared Object reference resolves to the single Project-level definition; Initial State does not duplicate that definition as independently writable Map data.
- A Shared Object may exist at Project scope but be absent from a Map's Initial State.
- Adding/removing a reference in one Map changes only that Map's membership and does not create/delete the Shared Object or alter other Maps.
- Branches inherit the root Map Initial State; they do not copy it or create a second time-zero baseline.
- Initial State edits use dedicated baseline-authoring operations, not post-zero Events or ordinary timeline Commands.
- A baseline edit invalidates affected reconstruction artifacts and Checkpoints for that Map and its descendant Branches; existing Commands/Events are preserved and then validated under the normal fail-fast reconstruction lifecycle.

## 4. Post-zero runtime history

- Post-zero Event-driven runtime changes belong to the currently active Map/Branch's selected History path.
- Only one Map is active at a time; other Maps remain silent and do not execute the active Map's Events.
- A post-zero Event does not mutate the Project-level Shared Object definition.
- Runtime state for the same Shared Object identity may differ across Maps/Branches.
- A selected Map/Branch is reconstructed through its one accepted History inheritance path. Do not compose a second Shared Object History stream.

## 5. Reconstruction input contract

For a selected Map/Branch at Historical Time T, Core Domain resolves:

1. Current Project-level definitions for Shared Objects referenced by the Map;
2. the Map's authoritative time-zero Initial State;
3. the selected History inheritance path and accepted fork boundaries;
4. applicable post-zero Events, Commands and State Changes on that path up to T, in deterministic Core Domain order;
5. required immutable assets.

The result is one World State for the selected Map/Branch. Checkpoints are derived caches and must match the complete reconstruction input context.

## 6. Invalidation and publication

| Change | Affected scope | Required action |
|---|---|---|
| Shared Object definition edit | Every referencing Map and affected descendant Branches | Invalidate derived reconstruction/Checkpoint context; prevent stale in-flight results from publishing; rebuild active path and inactive paths on demand under Core Domain policy |
| Map Initial State edit | That Map and all Branches rooted in it | Invalidate affected path Checkpoints; validate/reconstruct without rewriting authored history |
| Map post-zero Event/State Change | That Map's applicable path and descendants inheriting the change | Invalidate only affected paths |
| Branch-local Event/State Change | That Branch path and descendants inheriting the change | Invalidate only affected paths |
| Map reference membership edit | That Map and its descendant Branches | Invalidate only that Map's affected paths |

A shared-definition edit may invalidate many derived artifacts; it does not cause Maps to execute the same Event or acquire identical runtime states.

## 7. Forbidden representations

V1 must not introduce:
- a separately replayed Shared Object History stream;
- per-Map copies of a Shared Object definition;
- per-Map Commands generated to mirror one shared-definition edit;
- global mutable runtime-state rows shared by all Maps;
- AI-owned shadow definitions, history overlays or Checkpoints;
- Object Version or Shared Object revision-chain entities.

## 8. Acceptance scenarios

1. Two Maps reference one Shared Object; definition edits are visible in both.
2. Reconstructing either Map at a past Historical Time after the edit uses the current definition.
3. A post-zero Event in Map A does not change Map B's runtime state.
4. The same Shared Object may have different runtime states in A and B at the same Historical Time.
5. A Project-level Shared Object can be absent from a Map's Initial State.
6. Initial State membership changes do not create/delete shared identity or affect another Map.
7. Branches reuse the root Map Initial State and obey the frozen fork boundary.
8. Shared-definition edits invalidate affected Checkpoints and block publication of reconstruction results computed against stale definition inputs.
9. Shared definition edits do not create timeline Events or per-Map Commands.
10. ADR-003 A10-09 deletion-followed-by-modification behavior remains unchanged.

## 9. Open physical design items

- exact field ownership metadata and normalized/aggregate representation;
- audit metadata shape and retention;
- fan-out invalidation query/index strategy;
- reconstruction generation/fingerprint mechanism and in-flight publication barrier;
- exact Initial State record granularity and asset references;
- Domain Service APIs and transaction boundaries.

## 10. Status

The semantic rules in §§2–4 are accepted for V1 design. Physical keys, tables, indexes, migration strategy and implementation remain open. This document is not executable DDL or implementation authorization.