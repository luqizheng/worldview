# World View P2-09-08 — Command Descriptor and Deterministic Replay Contract V0.2

- Version: 0.2
- Date: 2026-11-21
- Status: Reconciled descriptor/replay contract — recommended baseline; concrete payload schemas and handler algorithms remain open
- Supersedes: P2-09-08 V0.1 for catalog scope and registration decisions; V0.1 is retained as history
- Depends on: ADR-001/002/003/005; P2-01-D01 D01-01~04, D01-08~16, D01-50~64; P2-09-03; P2-09-05; P2-09-07.

## 1. Purpose

Define the persistence and compatibility contract for typed Core Domain Commands. This document does not invent the final list of Command types or their domain payloads. It establishes what every registered Command type must declare so that Commands remain author-editable, executable, and historically reproducible.

## 2. Core decisions

1. A Command stores the author's operation/intent, not a serialized resulting World State.
2. Command types are explicitly registered in a Core Domain-owned descriptor catalog.
3. Each persisted Command identifies the descriptor contract and execution semantics needed to interpret it.
4. A Command may produce zero, one, or multiple State Changes.
5. A continuous Command may produce multiple time-phased State Changes; these are derived results, not independent user Commands.
6. Animation frames and visual interpolation are not State Changes unless they represent a genuine historical/domain state transition.
7. Algorithm or descriptor upgrades must not silently change the reconstructed meaning of existing history.
8. Command editing, deletion and insertion remain Core Domain authoring operations; no Command Revision Chain is introduced.

## 3. Command descriptor catalog

### 3.1 Ownership and deployment

- The descriptor catalog is owned by Core Domain.
- V1 should use a statically registered catalog compiled into the application, not a dynamic plugin marketplace or database-driven generic workflow engine.
- AI Assistant queries the catalog through Core Domain services and may submit only registered Command types.
- The catalog exposes metadata and validation contracts; it does not grant AI direct access to Command persistence.

### 3.2 Required descriptor identity

Every registered descriptor must define:

- stable Command Type ID, independent of display name;
- descriptor schema version;
- execution semantics/handler version or equivalent compatibility identifier;
- human-readable name and description;
- supported target scope (Map root History, Branch History, or other explicitly permitted scope);
- typed payload schema and validation rules;
- Historical Time requirements and precision/duration support;
- Object/Asset reference requirements;
- whether the Command can be continuous;
- State Change generation semantics and possible cardinality;
- preconditions and domain invariants;
- ordering/dependency declarations where applicable;
- deterministic replay and compatibility policy;
- editability constraints and reconstruction impact declaration.

This is a conceptual descriptor contract, not a prescribed class/property layout.

### 3.3 No generic CRUD command catalog

Do not define the formal Command model as generic Create/Update/Delete/Replace Geometry database operations. User-facing intent labels may use such words, but each submitted proposal must resolve to a registered semantic Command type.

The catalog must preserve the separate Object Operations and Geometry Operations systems:

- Geometry operations change Geometry Components and do not implicitly change semantic Object identity or count.
- Object operations change semantic Object identity, membership, count or relationships under explicit domain rules.
- A geometry split does not automatically create new Objects.
- A geometry merge does not automatically merge Object identities.
- Copy Object creates a new semantic identity and requires a new Object ID reservation.

Concrete command types should be introduced only when their domain semantics and validation behavior are specified. Do not pre-build speculative type families or generic extension frameworks.

## 4. Persisted Command information

A persisted Command conceptually contains:

- stable Command identity;
- exactly one owning History;
- stable Command Type ID;
- descriptor schema version;
- execution semantics/handler compatibility identifier;
- typed operation payload;
- Historical Time/effective-time input as required by the descriptor;
- duration/end-time input for continuous Commands;
- deterministic ordering metadata and explicit relation references where applicable;
- formal Object and immutable Asset references;
- optional Event association or narrative result reference;
- transaction-time audit metadata for creation/editing.

The Command must not store a complete mutable World State snapshot as its authoritative result. It may store necessary execution inputs or deterministic evidence required by the descriptor.

## 5. Descriptor and algorithm version compatibility

D01-04 requires historical consistency: an algorithm upgrade must not silently change the result of existing history.

Recommended V1 rules:

1. Persist the descriptor schema version used to validate and interpret the Command payload.
2. Persist an execution semantics/handler compatibility identifier sufficient for Core Domain to select the intended historical behavior.
3. Keep old deterministic handlers available for as long as persisted Commands require them, where feasible.
4. If old handlers cannot be retained, require an explicit, controlled history migration/re-authoring operation with before/after validation; never silently reinterpret old Commands using the newest algorithm.
5. A display-name or documentation-only descriptor change does not require a new semantic version if payload interpretation and execution behavior are unchanged.
6. A payload schema change that preserves meaning may use a compatible schema migration; a change that alters meaning requires a new descriptor/semantics version.
7. Rebuilding derived State Changes must use the Command's recorded execution semantics, not simply the currently latest handler.

This is descriptor/algorithm compatibility metadata, not a Command Revision Chain, Branch Revision, Object Version or World State Version.

The exact policy for retaining historical executable code, migrating old Command payloads, and handling unsupported legacy handlers remains an implementation decision that must be settled before production DDL and release policy are frozen.

## 6. Continuous Command contract

A Command may span a duration on the Historical Time axis.

- Persist the Command once as the authoritative operation.
- Its descriptor defines the start, duration/end boundary, and deterministic progression semantics.
- Reconstruction may derive multiple intermediate State Changes from the continuous operation.
- Those intermediate State Changes are derived execution results, not separately authored Commands.
- Visual animation/interpolation samples are not persisted as State Changes unless they correspond to domain-significant historical transitions.
- Pause/resume semantics must distinguish the world's Historical Time progression from the Command's own execution progress, as defined by the existing continuous-Command contract.
- A continuous Command must be deterministic at any requested reconstruction time, including times between its start and end.

A duration Command cannot rely on wall-clock time, frame rate, random unseeded behavior, or nondeterministic external services during replay.

## 7. Deterministic execution requirements

A Command handler must be deterministic for the same:

- Command payload;
- descriptor schema and execution semantics version;
- initial/effective target context;
- applicable prior history and ordering constraints;
- immutable referenced asset contents;
- explicitly captured deterministic inputs (including seeds or rule data, if the domain semantics require them).

Do not use transaction timestamps, machine-local time, current locale, UI state, network responses, unseeded randomness or execution scheduling as hidden inputs to historical outcomes.

If an operation fundamentally depends on external data, persist the exact immutable input/reference needed to reproduce it, or treat the result as an explicitly authored input rather than silently querying the external source during replay.

## 8. State Change materialization

The Command remains the authoring authority. State Changes are generated/derived according to the descriptor.

- A Command may yield zero State Changes.
- A Command may yield one State Change.
- A Command may yield multiple State Changes, including phase changes from a continuous Command.
- State Change generation is deterministic under the recorded execution semantics.
- If State Changes are persisted as a materialized projection, each record must retain producing Command provenance and the semantics identifier used to derive it.
- A regenerated projection is published atomically; partial output is never current.
- A stale projection must not be consumed by reconstruction.
- If the chosen implementation derives State Changes in memory, it must still guarantee deterministic replay and preserve any required diagnostics/evidence.

No independently editable State Change authoring ledger is introduced.

## 9. Command edit, delete and insertion

- Editing a Command updates its authoritative operation record under Core Domain rules.
- Deleting a Command removes that authoring operation; dependent Commands and explicit ordering relations are retained and reevaluated according to Core Domain rules.
- Inserting a Command at an arbitrary Historical Time preserves later Commands and triggers dependency/order reevaluation from the earliest affected point.
- Changing Historical Time rebuilds from the earlier of the old and new effective positions.
- Rebuilding invalidates affected State Change materializations and Checkpoints.
- Commands after a failed reconstruction boundary remain persisted but are not treated as successfully reconstructed.
- Normal editing mode and restricted failed-history repair mode retain the existing D01 permissions and boundaries.

The exact edit authorization rules for user-authored versus AI-submitted Commands are outside this persistence contract; AI authorization evidence remains immutable and cannot be reused for later edits.

## 10. AI Assistant integration

AI Assistant must:

1. Query Core Domain for currently supported Command descriptors.
2. Use the exact stable Command Type ID, schema version and semantics identifier supplied by Core Domain.
3. Generate a typed proposal payload conforming to that descriptor.
4. Obtain Core Domain preflight validation and target History resolution.
5. Seal proposal content and obtain human authorization where required.
6. Submit only through Core Domain Domain Services.

AI Assistant must not:
- invent Command Type IDs or payload schemas;
- execute Command handlers itself;
- generate authoritative State Changes;
- upgrade or rewrite persisted Command semantics;
- write formal Commands or Events directly;
- treat AI Candidate geometry/attributes as resulting World State.

## 11. Minimum verification scenarios

1. Unknown Command Type ID is rejected before persistence.
2. Payload incompatible with its descriptor schema version is rejected.
3. Command persists with exact descriptor and execution semantics identifiers.
4. A Command produces zero State Changes.
5. A Command produces multiple State Changes deterministically.
6. A continuous Command produces the same state at the same intermediate Historical Time across repeated rebuilds.
7. Visual interpolation does not create historical State Changes.
8. A code/algorithm upgrade does not change an old Command's result silently.
9. A compatible payload migration preserves semantic result and records its migration path.
10. An incompatible semantic change requires a new descriptor/semantics identifier or explicit controlled history migration.
11. Editing a Command invalidates affected derived output without creating a Command Revision Chain.
12. Deleting a Command retains dependent Commands and re-evaluates their execution/ordering.
13. Inserting a Command retains later Commands and deterministically rebuilds the affected range.
14. A Branch cannot accept an owned State Change at or before its parent_at boundary.
15. Geometry split/merge does not implicitly allocate or merge Object identities.
16. AI Assistant cannot submit an unregistered or guessed Command type.

## 12. Open items before physical DDL

The bounded recommended V1 catalog is now recorded in P2-09-15 V0.1. The following remain open:

1. Concrete typed payload schemas and canonical serialization for each enabled descriptor.
2. Exact descriptor storage strategy (compiled registry is recommended; no new descriptor table is currently proposed).
3. Historical handler retention and explicit migration policy.
4. Exact continuous Command phase-generation algorithm and pause/resume data for future continuous descriptors.
5. Exact State Change persistence/materialization choice (V1 in-memory derivation remains the current recommendation).
6. Deterministic inputs required by geometry, movement, terrain and other future domain operations.
7. Command/Event/State Change association details for each concrete Command type.

## 13. Status

This contract establishes the descriptor and deterministic replay requirements for P2-09. P2-09-15 V0.1 supplies the current bounded recommended V1 catalog; neither document claims that all candidate descriptors, payloads or handlers are frozen or implemented. This is not executable DDL or implementation authorization.