# World View Core Domain P2-08 — Shared / Local History Reconciliation Review

- Version: 0.1
- Date: 2026-11-21
- Status: Design review — formal Core Domain decision required

## 1. Executive finding

The current approved Core Domain decision and later proposed domain-model discussions contain a material semantic conflict. This must be resolved by the Core Domain authority before the AI Assistant persistence schema is finalized.

ADR-003 (Accepted / Frozen) specifies that a Shared Object has its own Shared Object History; edits in that history affect every Map referencing the object; Map-local edits belong to the current Map-local History; and playback combines applicable Shared and Map-local Commands into one deterministic execution sequence.

D01-70 and D01-76 (Proposed / Discussion) describe a different model: shared identity/base data does not imply a shared mutable runtime state; local mutations are Commands along the selected Branch's history path; the same Shared Object may therefore have different runtime states in different Branches. D01-76, including the user's later clarification, rejects composing two independent authoritative streams into a selected Branch's playback.

D01-76 is important evidence of intended direction, but it is not an Accepted replacement for ADR-003. The AI Assistant design must not silently treat it as one.

## 2. Existing decisions and their status

### 2.1 ADR-003 — Accepted / Frozen

The relevant frozen semantics are:

1. A Shared Object owns a Shared Object History.
2. A Shared Object mutation belongs to that Shared Object History and affects every Map referencing the object.
3. A Map-local mutation belongs to that Map's local History and affects only that Map.
4. Playback of one selected Map or Branch executes applicable Shared Object Commands and Map-local Commands as one deterministic continuous sequence, producing one state.
5. A playback selects one Map or Branch; it does not simultaneously play multiple Branches.
6. A10-09 specifies that after a shared object is deleted, a later local edit may still execute but must not resurrect the object; a persistent quality warning is produced.

ADR-003 explicitly leaves parts of command structure, history storage, playback composition, and Shared/Local implementation to subsequent technical design. That delegation does not authorize implementation to reinterpret the frozen product semantics.

### 2.2 D01-70 and D01-76 — Proposed / Discussion

The later proposed direction establishes that:

- Shared identity and base data do not mean all Branches share one mutable runtime state.
- A local mutation is represented by a Command on the selected Branch's history path.
- A selected Branch is reconstructed through one continuous inheritance path.
- The same Shared Object can have different runtime states in different Branches.
- D01-76 rejects a second independent Shared Object History stream being interleaved with Branch-local Commands to produce the selected Branch state.

However, D01-76 does not fully specify how the global propagation behavior required by ADR-003 is retained, replaced, or deliberately removed.

## 3. Exact semantic tension

| Concern | ADR-003 (Frozen) | D01-70 / D01-76 (Proposed) |
|---|---|---|
| Global shared mutation owner | Shared Object History | Rejects an independent Shared Object History as a second authority |
| Propagation to all Maps | Yes | No complete replacement rule stated |
| Local mutation owner | Map-local History | Command on the selected Branch history path |
| Runtime state across Branches | Playback composition defined around Shared + Local streams | Same Shared Object may have different Branch-specific runtime states |
| Reconstruction | Applicable Shared + Local Commands form one sequence | One selected Branch inheritance path; no independent streams to interleave |

This is not merely a storage-layout question. It determines the product semantics of editing shared objects and the meaning of historical reconstruction.

## 4. Designs that must not be introduced as a workaround

The AI Assistant and persistence design must not:

1. Add an independent `SharedObjectHistory` table/stream and merge it with Branch-local Commands at runtime. This directly conflicts with D01-76's proposed single-path model.
2. Silently remove ADR-003's global shared-mutation semantics without an explicit replacement decision.
3. Copy shared Commands into every Map or Branch. This creates duplicate authority and conflicts with the no-copy inheritance model.
4. Store one shared mutable runtime state used by all Branches. This prevents Branch-specific state.
5. Create an AI-owned shadow history, overlay, or third mutation stream to reconcile the disagreement.
6. Let the AI infer whether a user's edit is global or local when that distinction changes the outcome. Material ambiguity must be surfaced for a domain decision or user clarification.

## 5. Semantic directions for the Core Domain decision

These are decision directions for the Core Domain owner, not a decision made by the AI Assistant design.

### Option A — Retain true global shared mutations

Retain the ADR-003 behavior that a shared mutation affects every Map referencing the object. The Core Domain must define how that global effect is represented in each selected Map/Branch's single continuous reconstruction path without:

- creating a second independently authoritative stream;
- copying Commands into every Branch;
- sharing mutable runtime state between Branches; or
- introducing a shadow overlay.

The decision must define historical propagation, fork-boundary behavior, edits to past shared mutations, and deterministic ordering relative to Branch-local Commands.

### Option B — Shared identity/base data only

Treat sharing as shared identity and base data only. All runtime mutations are owned by a Map or Branch and occur along its history path. This is closest to D01-70/76, but it changes ADR-003's global propagation semantics.

The Core Domain must explicitly decide whether global multi-Map changes are removed, replaced by an explicit operation that targets multiple Maps, or handled by another formally specified mechanism. It must also specify how existing shared-object data and histories are migrated or interpreted.

### Option C — Explicit hybrid semantics

Define distinct mutation classes with explicit ownership and propagation rules. For example, the domain may distinguish a global shared-definition change from a Branch-local runtime-state change. This is viable only if the Core Domain specifies exactly how both classes participate in one selected Branch reconstruction path, including time, ordering, historical edits, deletion, and fork behavior.

A hybrid must not become an implicit third history stream or an ambiguous owner flag with undefined semantics.

## 6. Required Core Domain integration contract

Regardless of the selected option, the Core Domain must expose an authoritative projection for a selected Map or Branch. At minimum it must provide:

- the exact applicable Commands for that selected reconstruction;
- the owner and scope of each Command (as domain-defined opaque identifiers or explicit semantics);
- the selected inheritance path and each relevant fork boundary;
- the applicable Historical Time interval and deterministic execution order;
- resolved target identity and object availability/deletion semantics;
- dependency and conflict diagnostics relevant to the sequence; and
- reconstruction and playback outcome, including the boundary between persisted history and derived state.

The AI Assistant consumes this projection. It must not independently compose Shared and Local streams or invent a second interpretation of Branch inheritance.

## 7. Required ADR resolution topics

The Core Domain should issue a new ADR or formally replace/revise ADR-003. The decision must explicitly address:

1. Whether true global shared mutation remains a supported product capability.
2. The authoritative owner of each mutation class.
3. How global changes propagate to every referencing Map, if retained.
4. Whether and how Branches may have different runtime states for the same Shared Object.
5. How deletion interacts with later local Commands, preserving or replacing A10-09.
6. How multi-Map edits are represented and authorized.
7. Historical Time, fork-boundary, ordering, and historical-edit behavior.
8. Compatibility and migration implications for existing data and Commands.
9. The exact projection contract consumed by reconstruction, playback, and AI Assistant.

## 8. AI Assistant and database implications

Until this decision is made:

- Keep target History, scope, and ownership references opaque in AI-owned records.
- Do not create AI-owned Shared/Local history tables.
- Do not freeze AI schema owner discriminators, Core Domain FKs, or mutation-specific table relationships that presume one option.
- Require the Core Domain to resolve target ownership/scope before a Command Proposal is sealed and authorized.
- If the user's intent is materially ambiguous between global and local effects, request clarification rather than guessing.

The AI Assistant remains responsible for intent interpretation, proposal preparation, exact-version authorization evidence, and submission tracking. The Core Domain remains the sole authority for formal Commands and reconstruction semantics.

## 9. Gate and recommendation

**P2-08 status: OPEN — BLOCKING for final physical persistence design.**

This review intentionally does not choose Option A, B, or C. The disagreement changes domain behavior and must be resolved by the Core Domain decision authority. Once resolved, update the Core Domain ADR and integration contract, then reconcile the AI Assistant logical schema and persistence proposal against that authoritative decision.

## 10. Related documents

- ADR-001 — Map History and Branch Model (Accepted / Frozen)
- ADR-002 — Unified History Entity (Accepted / Frozen)
- ADR-003 — Command-Driven History (Accepted / Frozen)
- ADR-005 — Branch Fork Rules (Accepted / Frozen)
- D01-70 through D01-76 — Shared Object / Branch discussions (Proposed / Discussion unless separately accepted)
- World View AI Assistant Core Domain Integration Contract V0.2
- Command Proposal and Authorization Design V0.1
- Command Editing and Reconstruction Lifecycle V0.1
- Core Domain Persistence Discovery and AI Schema Redesign Plan V0.1