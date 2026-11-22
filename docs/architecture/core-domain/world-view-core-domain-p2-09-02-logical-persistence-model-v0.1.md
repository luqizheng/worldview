# World View P2-09-02 — Core Domain Logical Persistence Model

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — recommended logical model; not frozen physical schema or implementation authorization
- Scope: Core Domain logical persistence model for the new C# + Avalonia, single-user, local-first World View application
- Depends on: P2-09-01 Persistence Ownership Model; Core Domain Architecture Baseline V1.0; ADR-001~005; P2-01 Domain Model Proposal; current user-confirmed Shared Object / Event semantics

## 1. Executive recommendation

Persist the **authoritative inputs required to reconstruct a Map or Branch**, not mutable copies of the resulting world state.

The Core Domain logical persistence model is organized around these concepts:

1. Project and Map identity/configuration.
2. Stable Object identity, Object Type identity, and permanent Object ID reservations.
3. Shared Object definition/base data and Map references to shared objects.
4. Initial Map state at the Map's time-zero point.
5. Unified History identity and Branch inheritance declarations.
6. Authoritative authoring Commands, Events, and explicit ordering constraints.
7. State Change semantics needed by the reconstruction engine, with the exact persistence/derivation boundary owned by Core Domain design.
8. Immutable asset payloads and authoritative asset references.
9. Checkpoint metadata and file-backed derived payloads.
10. Core Domain schema/migration metadata.

World State, current ObjectState, playback frames, previews, indexes, and Checkpoint payloads are derived. They must never replace the authoritative history and initial-state inputs.

This document deliberately defines conceptual records and relationships, not SQL table names, columns, primary-key encodings, indexes, or migration scripts.

## 2. Architecture and ownership constraints

- Target runtime: C# + Avalonia. Rust + Bevy is discontinued; existing Rust code is historical reference only.
- One local Project persistence unit; no multi-user, distributed, or multi-instance assumptions in V1.
- SQLite remains the recommended relational authority, subject to provider/transaction validation in later P2-09 work.
- Core Domain alone owns and writes formal Map, Object, Shared Object, Branch, History, Command, Event, State Change, reservation, asset-reference, and Checkpoint metadata.
- AI Assistant owns Task/Candidate/Proposal/Authorization/Submission evidence and accesses Core Domain through Domain Services.
- One SQLite file does not mean unrestricted cross-module SQL. Module ownership and migration ownership remain separate.
- Large immutable geometry and other payload bytes may live in the project-local Asset Store. The Core Domain owns their semantic references.
- No Object Version, World State Version, Branch Revision Chain, Command Revision Chain, merge entity, or AI shadow history is introduced.

## 3. Logical aggregate map

The following is a conceptual ownership view. It is not a mandatory one-table-per-box mapping.

```text
Project
 ├─ Maps
 │   ├─ Map configuration / Calendar
 │   ├─ Root History
 │   ├─ Initial Map State (time zero)
 │   ├─ Map Object references / registrations
 │   └─ Branches
 │       ├─ Branch declaration: parent History + parent_at
 │       └─ Branch-owned History
 ├─ Shared Object definitions
 │   └─ Map-to-Shared-Object references
 ├─ Object identity registry + Object ID reservations
 ├─ Command catalog / Event / ordering constraints
 ├─ Asset references
 └─ Checkpoint metadata
```

A Map and a Branch both resolve to a History. The Branch does not copy ancestor Commands or Events; it references the parent History and fork boundary.

## 4. Logical records and responsibilities

### 4.1 Project

Represents one local World View project and its Core Domain persistence boundary.

Logical information:
- stable Project identity;
- project display metadata and lifecycle state;
- schema compatibility information (physical migration ledger is specified later);
- project-level configuration references.

Project identity is not an AI Task or workspace identity. Application preferences and window state are outside formal map history.

### 4.2 Map

Represents a world-creation container.

Logical information:
- stable Map identity;
- Project ownership;
- map metadata and lifecycle state;
- the Map's primary Calendar/time model reference;
- its root History reference;
- the identity of its time-zero Initial State boundary.

A Map is not a snapshot of a Branch and is not a mutable World State row.

### 4.3 Calendar and Historical Time

Calendar defines the interpretation and ordering semantics of Historical Time for a Map and its descendants.

Logical information:
- stable Calendar identity and definition;
- epoch/time-zero meaning;
- canonical ordered time value;
- display/formatting metadata, where needed;
- precision and duration semantics supplied by the Core Domain.

Constraints:
- one Map has one primary Calendar in V1;
- Branches inherit the same Calendar and cannot silently switch calendars;
- Historical Time is separate from transaction timestamps such as created_at, edited_at, authorized_at, and submitted_at;
- Branch-owned Commands/State Changes must satisfy `effective_time > parent_at`;
- same-time ordering must be deterministic and preserve explicit user constraints.

The exact canonical storage representation remains a Core Domain contract. D01 discussion points toward cumulative integer days plus an integer within-day offset (currently seconds), but this is not frozen as a physical representation here.

### 4.4 Object Type and Object identity

Object Type defines the category under which Object IDs are allocated. Object identity is stable and does not contain mutable runtime state.

Logical information:
- stable Object Type identity;
- stable Object identity;
- the owning/creating Map or shared-object registration context;
- creation/registration provenance;
- lifecycle and identity-resolution metadata.

Constraints:
- Object identity is distinct from ObjectState;
- Object geometry and attributes are not overwritten as current-state columns on the Object identity record;
- geometry split/merge operations do not silently create, destroy, or merge semantic Object identities;
- an Object reference may be used by a Map/Branch history when the Core Domain's ownership and sharing rules permit it;
- stable Object IDs are allocated by Core Domain according to Object Type, never by AI Assistant.

### 4.5 Object ID Reservation

Reservation is a durable identity-allocation fact, not temporary AI staging metadata.

Logical information:
- reservation identity;
- Object Type;
- reserved Object ID;
- reservation state;
- associated draft/object binding where applicable;
- creation and transition timestamps;
- abandonment reason/evidence where applicable.

Lifecycle:
- `Reserved` — ID allocated and unavailable to all other objects;
- `Bound` — reservation associated with a specific Object draft/identity;
- `Inserted` — formal Object creation has been committed;
- `Abandoned` — reservation permanently consumed but not inserted.

Rules:
- once reserved, an ID is never returned to the allocation pool;
- `Abandoned` IDs are never reused;
- Task failure, cancellation, Candidate rejection, or Task Undo does not reverse reservation consumption;
- retry/recovery reuses the existing reservation and binding rather than allocating a replacement ID;
- AI Assistant may request a reservation and retain its opaque reference, but cannot allocate, recycle, or mutate reservation state directly.

### 4.6 Shared Object definition and Map references

This section records the user's clarified business semantics as the current working baseline for P2-08 integration.

Two distinct kinds of information must not be collapsed:

**A. Shared Object definition/base data**
- A Shared Object has one shared identity/definition.
- Maps that reference it use the same shared definition/base data.
- A change that edits the Shared Object itself is common to every Map referencing it.
- The logical model therefore needs a shared-object identity/definition concept and explicit Map references, rather than copied independent definitions in every Map.

**B. Post-zero Event-driven runtime changes**
- Time zero is the initial point of the Map, not an arbitrary later time.
- After a Map's timeline has started, a change caused by an Event belongs to the currently active Map/Branch history.
- Only one Map is active at a time. Other Maps are silent/inactive and do not execute that Event.
- Consequently, executing a post-zero Event in one Map does not change the reconstructed runtime state of the same Shared Object in another Map.
- The same shared identity/base definition can therefore have different reconstructed runtime states in different Maps/Branches because their own histories differ.

Persistence consequences:
- do not represent a post-zero Event as a global shared-definition update;
- do not create a second independently authoritative SharedObjectHistory stream and merge it with Branch history;
- do not copy a shared Command into every Map/Branch;
- do not store one mutable runtime-state row shared by all Maps;
- preserve the distinction between shared definition/base data and each Map's time-zero initial state and later Event-driven history effects.

The exact formal mapping between shared-definition edits and ADR-003's prior global shared-mutation wording still requires the minimum ADR reconciliation. This document does not invent an additional history stream or silently claim the ADR text has already been amended.

### 4.7 Initial Map State (time zero)

Initial State is the authoritative starting input for reconstructing a Map at its time-zero point.

Logical information:
- Map identity;
- the Map's time-zero boundary;
- the initial Object membership/availability relevant to that Map;
- initial attributes, geometry and relationships;
- references to immutable asset payloads where data is large;
- references to Shared Object definitions where applicable.

Rules:
- `reconstruct(Map, time_zero)` yields the Initial State;
- Initial State is not a Command and is not a post-zero Event;
- time-zero shared-object base information must resolve to the common Shared Object definition/base data;
- Map-specific initial membership/placement/context remains associated with the Map;
- later state changes are expressed through the Map/Branch history, not by overwriting Initial State;
- whether initial-state editing is modeled as direct baseline authoring or an explicit pre-history operation must be specified by Core Domain authoring rules; it must not be confused with post-zero Event execution.

### 4.8 History

History is the unified historical container used by both a Map root and a Branch-owned history.

Logical information:
- stable History identity;
- owning Map/Branch context;
- root-versus-Branch role or equivalent ownership relationship;
- authoritative authoring Commands;
- Events belonging to this History;
- explicit ordering constraints belonging to or inherited by this History according to Core Domain rules.

Constraints:
- a Map has one root History;
- a Branch has one own History and references its parent History plus `parent_at`;
- Branch creation is zero-copy: ancestor Commands/Events are not duplicated;
- ancestor history at or before the fork boundary is inherited; ancestor history after the boundary is not;
- Branch-owned changes must be later than `parent_at`;
- edits to ancestor history inside the adopted inheritance boundary can affect descendants; edits outside the boundary do not;
- History is the authority for authoring history, not a cache of current state.

### 4.9 Branch

Branch is a declaration of an alternate continuation from a parent History at a specific Historical Time.

Logical information:
- stable Branch identity and metadata;
- parent History reference;
- `parent_at` Historical Time;
- its own History reference;
- status such as active/deprecated, according to the accepted domain rules.

Constraints:
- parent points to a History, not necessarily directly to a Map;
- Branch-of-Branch is supported as a tree; cycles are forbidden;
- no Merge/Rebase/Cherry-pick/Conflict Resolution entities are introduced;
- Branch does not copy ancestor Objects, Commands, or Events;
- Branch-owned state changes are strictly after `parent_at`;
- a Branch's own changes never mutate its parent or sibling Branches;
- the exact branch deletion constraints follow the accepted Core Domain rules and are not expanded by this persistence model.

### 4.10 Command

Command is the authoritative record of an author's history-creation action.

Logical information:
- stable Command identity;
- owning History;
- registered Command type/descriptor identity and descriptor version where needed;
- typed semantic payload;
- Historical Time/effective-time information where applicable;
- deterministic sequence/order metadata;
- references to target Objects and immutable assets;
- optional Event association or other domain-defined links;
- transaction-time audit metadata needed for authoring and diagnostics.

Constraints:
- Commands may be edited, deleted, and inserted according to Core Domain authoring rules;
- no Command Revision Chain is introduced;
- editing/deleting/inserting Commands triggers the Core Domain's history validation/reconstruction lifecycle;
- a Command may produce zero, one, or multiple State Changes;
- formal Commands are written only through Core Domain Domain Services;
- AI Command Proposals are not Commands and remain AI-owned until authorized and submitted.

### 4.11 Event

Event is a historical/semantic record of what happened in the modeled world. It belongs to one History.

Logical information:
- stable Event identity;
- owning History;
- event type and typed payload/description;
- Historical Time and ordering context as defined by Core Domain;
- optional association with one or more Commands/State Changes where the domain contract permits.

Constraints:
- an Event is not the parent object of a State Change;
- Event does not automatically mutate a Shared Object globally;
- a post-zero Event's effects are executed only in the currently active Map/Branch reconstruction path;
- the precise relationship between Event records, Commands, and State Changes must follow the final Core Domain command/event contract; do not assume a one-to-one relationship.

### 4.12 State Change

State Change represents the state-change semantics expressed or produced by a Command. It is not assumed to be a second independent authoring ledger.

Logical information may include:
- stable State Change identity when the Core Domain chooses to persist it;
- source Command identity;
- owning History or derivation context;
- target Object identity;
- effective Historical Time and deterministic sequence;
- registered change type and typed payload;
- optional Event association.

Important persistence decision:
- The Core Domain must decide whether State Changes are persisted as first-class derived/executable records, deterministically regenerated from Commands, or represented through another internal structure.
- Whichever representation is chosen, Commands remain the authoring authority and State Changes must not become a competing authoring ledger.
- Do not collapse Command and State Change into one concept merely to simplify SQL.
- Do not assume one Command equals one State Change.

### 4.13 Explicit ordering constraints

User-authored ordering constraints are authoritative domain data. Automatically discovered dependencies are derived and recalculable.

Logical information:
- stable constraint identity;
- source/target Command or ordering endpoint references;
- constraint direction and semantic kind;
- authoring provenance and transaction-time metadata;
- status/diagnostics may be derived from current history evaluation.

Rules:
- explicit constraints are preserved even if they create a cycle;
- cycles and affected dependencies are diagnosed; affected Commands are paused rather than silently deleting constraints;
- inherited parent constraints remain authoritative and cannot be overridden by a child Branch;
- a Branch may add its own constraints within the allowed scope;
- deterministic technical tie-breakers are allowed only when they cannot change semantic results;
- if different valid orders produce different outcomes and intent is unclear, ask the user;
- implicit dependencies are recalculated and are not promoted to permanent user-authored constraints without explicit acceptance.

### 4.14 Immutable assets and asset references

The Asset Store stores immutable bytes; Core Domain owns the semantic references required to reconstruct formal data.

Logical asset-reference information:
- stable asset identity or content hash;
- payload category and media/format identifier;
- format version;
- cryptographic content hash (SHA-256 recommended);
- byte size;
- relative project-local storage locator;
- integrity/publication status;
- semantic owner/reference links from Initial State, Commands, Objects or Checkpoints as appropriate.

Rules:
- publish bytes immutably; never mutate a published asset in place;
- write/validate the asset before committing the SQLite reference;
- an unreferenced file after a failed database transaction is an orphan candidate, not a formal asset;
- cleanup must verify that no committed reference exists;
- AI staging assets remain AI-owned until promoted/referenced by Core Domain services;
- database and Asset Store are backed up/restored as one Project consistency unit.

### 4.15 Checkpoint

Checkpoint is a derived cache that accelerates reconstruction. It is not history authority and not a World State Version entity.

Logical information:
- stable Checkpoint identity;
- selected Map/Branch and Historical Time;
- complete inherited-path context/fingerprint supplied by Core Domain;
- payload format/version;
- immutable asset reference, content hash and size;
- generation/publication status and integrity status;
- reference/lifecycle metadata required to prevent eviction while in use.

Rules:
- Checkpoint is usable only when complete and its full inherited-path context matches;
- publish atomically: temporary output → validate → hash → immutable file publish → final lineage/context validation → complete metadata publication;
- logical invalidation and physical file deletion are separate;
- referenced Checkpoint files cannot be evicted until references are released;
- missing/corrupt Checkpoints trigger reconstruction from authoritative history;
- corruption of a Checkpoint does not invalidate formal history;
- active paths may rebuild automatically according to Core Domain policy; inactive paths rebuild on demand;
- AI Candidate/staging data is never stored in the Core Domain Checkpoint namespace.

The precise lineage fingerprint/version-combination algorithm remains Core Domain-owned. It must not be replaced with AI-owned Branch Revision or Object Version entities.

## 5. Authoritative versus derived persistence

| Information | Persistence role | Authority |
|---|---|---|
| Project, Map, Object, Object Type, Shared Object identity | Authoritative | Core Domain |
| Shared Object definition/base data | Authoritative shared definition | Core Domain |
| Map time-zero Initial State | Authoritative reconstruction input | Core Domain |
| History, Branch parent/fork declaration | Authoritative | Core Domain |
| Authoring Commands | Authoritative | Core Domain |
| Events | Authoritative historical records | Core Domain |
| Explicit ordering constraints | Authoritative user-authored data | Core Domain |
| State Change representation | Core Domain-defined executable/derived history data; not a competing authoring authority | Core Domain |
| World State / ObjectState at requested time | Derived reconstruction result; cacheable only | Core Domain reconstruction service |
| Playback frames and UI previews | Derived/transient | Application/Core Domain service |
| Checkpoint metadata | Derived-cache metadata | Core Domain |
| Checkpoint payload | Immutable derived file | Asset Store under Core Domain lifecycle |
| Asset bytes referenced by formal data | Immutable payload | Asset Store; semantic reference owned by Core Domain |
| AI proposals/candidates/authorization | Not Core Domain persistence | AI Assistant |

## 6. Required Core Domain read/projection contract

The Core Domain must expose a deterministic projection for a selected Map or Branch and requested Historical Time. Callers must not reconstruct the applicable history by querying tables independently.

At minimum the projection/service contract supplies:
- selected Map/Branch identity;
- selected History and complete inheritance path;
- each fork boundary and the effective inherited interval;
- applicable Commands/Events/State Change execution inputs in deterministic order;
- the ownership/scope of each applicable item, including shared-definition versus Map-local Event effects;
- target Object identity and availability/deletion semantics at the requested time;
- ordering/dependency/conflict diagnostics;
- Checkpoint eligibility and validity, if applicable;
- reconstruction result, first failure diagnostic, and latest valid playback boundary.

AI Assistant consumes this projection and the returned opaque references. It must not independently join or merge Shared and Local histories, calculate Branch inheritance, or create a second World State implementation.

## 7. Logical integrity constraints

The eventual physical schema and Domain Services must enforce at least these invariants:

1. Every Map has exactly one root History.
2. Every Branch has exactly one own History and one valid parent History plus `parent_at`.
3. Branch parent graph is acyclic.
4. Branch-owned Commands/State Changes cannot be effective at or before `parent_at`.
5. Ancestor history is inherited only within the accepted fork boundary; no ancestor Commands are copied.
6. Every formal Command belongs to exactly one History.
7. Every Event belongs to exactly one History.
8. A Command is not assumed to produce exactly one State Change.
9. Every formal Object identity is allocated/registered by Core Domain.
10. An Object ID reservation is unique within its Object Type allocation scope and never recycled after reservation.
11. `Abandoned` reservation remains permanently consumed.
12. Shared Object definition/base data has one authority; Maps reference it rather than maintain independently writable copies.
13. Post-zero Event effects belong to the active Map/Branch history and do not mutate another inactive Map's runtime state.
14. Explicit ordering constraints are preserved even when evaluation reports a cycle.
15. World State is reconstructed; it is not an authoritative mutable table.
16. Checkpoint is usable only when complete, intact, and valid for the full selected inheritance path.
17. Formal asset references cannot point to unpublished or integrity-failing assets.
18. AI Assistant cannot write Core Domain records except through the authorized Domain Service/Commit Coordinator boundary.

## 8. Physical schema design guidance (deferred)

When translating this logical model into SQLite:
- prefer a small number of normalized tables with explicit constraints;
- use stable opaque IDs; exact key type/encoding is a later decision;
- use typed payload columns/JSON only where the Command descriptor contract justifies it;
- keep large geometry/raster/payload bytes in immutable files where beneficial;
- index History ownership, Branch parent traversal, Historical Time/order, Object references, reservation lookup, and Checkpoint context only after query patterns are defined;
- enforce foreign keys and uniqueness where the actual ownership model is stable;
- avoid speculative table-per-Command-type designs;
- do not add a generic event-sourcing framework, outbox, message queue, distributed lock, or second writable database.

No physical table name or column in this document is an instruction to create a same-named SQL object.

## 9. Open design items to resolve before physical DDL

These are bounded technical/domain-contract items, not reasons to reopen the entire logical model:

1. **P2-08 formal ADR reconciliation:** update ADR-003 wording to distinguish Shared Object definition/base-data edits from post-zero Event-driven runtime effects, preserving the user's clarified behavior and explaining the exact relation to earlier global shared-mutation wording.
2. **Command/Event/State Change contract:** settle which State Change data is persisted versus derived from Commands, and the exact Event-to-Command/State Change associations.
3. **Command catalog:** define registered Command types, payload schemas, descriptor compatibility/versioning, and edit/delete/insert behavior.
4. **Historical Time and ordering:** freeze canonical time representation, precision, duration and explicit ordering storage.
5. **Object reservation:** define the exact Object Type allocation scope, reservation key format, uniqueness and atomic binding/insertion semantics.
6. **Initial State authoring:** define how edits to time-zero baseline are represented and validated without treating them as post-zero Events.
7. **Asset and Checkpoint contracts:** define file formats, complete marker, lineage context, publication and cleanup/reference rules.
8. **Physical keys and migrations:** derive actual SQLite schema, FK graph, migration ownership and bootstrap sequence.
9. **Provider and transaction proof:** validate the selected .NET SQLite provider and shared Core Domain/AI transaction boundary on Windows and macOS.

## 10. Decisions made in this document

Recommended working baseline:
- persist authoritative reconstruction inputs rather than mutable World State;
- model Project/Map/History/Branch/Object/Shared Object/Initial State/Command/Event/ordering/reservation/assets/Checkpoint as logical persistence concepts;
- keep Object identity separate from mutable ObjectState;
- keep Commands authoritative for authoring, with State Change as distinct semantics;
- use zero-copy Branch inheritance through parent History + `parent_at`;
- distinguish Shared Object definition/base data from post-zero active-Map Event effects;
- preserve Object ID reservation consumption permanently;
- store large immutable assets outside SQLite where appropriate;
- keep Checkpoints rebuildable and lineage-validated;
- require Core Domain to provide the authoritative selected-history projection.

Not decided/frozen here:
- exact physical tables, columns, key types, indexes and SQL constraints;
- provider/version;
- State Change physical representation;
- exact P2-08 ADR wording and compatibility interpretation;
- exact asset/Checkpoint file formats;
- migration sequence and transaction implementation.

## 11. Status and next step

P2-09-02 provides a coherent logical persistence model sufficient to proceed with bounded contract closure. It is not an executable SQLite schema, migration package, or implementation authorization.

Next, complete the highest-risk remaining contracts in this order:
1. P2-08 minimum ADR reconciliation for Shared Object / Event semantics;
2. Command, Event, and State Change persistence/derivation contract;
3. Object ID reservation persistence and binding contract;
4. Historical Time and ordering physical representation;
5. Asset and Checkpoint lifecycle contract;
6. integrated physical schema and migration design;
7. .NET provider and transaction proof before implementation handoff.