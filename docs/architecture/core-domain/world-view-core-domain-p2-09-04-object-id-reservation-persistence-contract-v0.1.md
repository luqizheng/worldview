# World View Core Domain — P2-09-04 Object ID Reservation Persistence Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — recommended allocation and lifecycle contract; physical schema and display format remain open
- Depends on: P2-09-01 Persistence Ownership; P2-09-02 Logical Persistence Model; AI Assistant Core Domain Integration Contract V0.2; established Object ID Reservation lifecycle decision.

## 1. Purpose

Define the durable identity-allocation semantics and persistence responsibilities for Object IDs. Reservation is a Core Domain identity fact, not an AI staging convenience.

## 2. Core decisions

1. **Core Domain exclusively allocates Object IDs.** AI Assistant, Candidate generation, plugins and UI code cannot allocate or fabricate formal IDs.
2. **Object Type controls the ID allocation namespace.** A reservation is allocated within one Object Type namespace.
3. **Recommended namespace scope is Project + Object Type.** This gives one stable identity namespace across all Maps in a Project, including Shared Objects referenced by multiple Maps. It avoids accidental duplicate IDs when an Object is shared across Maps. Map/Branch is not part of the allocation namespace.
4. **Allocation is monotonic and non-recycling.** Once an ID has been reserved, it is permanently consumed, regardless of whether the Object is ever inserted.
5. Reservation lifecycle is `Reserved → Bound → Inserted`, with `Abandoned` as a terminal outcome reachable from `Reserved` or `Bound`.
6. Reservation state and Object identity are separate concepts. A reservation is not itself an Object.
7. Reservation lifecycle is durable across Task failure, cancellation, Candidate rejection, rollback, application retry and process restart.

The Project + Object Type namespace is a recommended technical interpretation of the existing rule that Object IDs are allocated by Object Type. If the existing product identity contract already defines a different stable namespace, that contract must take precedence; do not silently switch namespaces after IDs have been issued.

## 3. Identity model

### 3.1 Object Type

Object Type defines the category and allocation namespace for formal Object IDs.

Conceptual responsibilities:
- stable Object Type identity;
- stable allocation namespace identity;
- ID formatting/display policy (if any);
- allocation sequence state or equivalent allocator state;
- compatibility rules if the display name/code changes.

Object Type display names and labels are not suitable as the sole namespace key because they may be renamed. Use a stable Object Type identity; the final key representation is a physical-schema decision.

### 3.2 Object ID

An Object ID is the permanent identifier allocated under one Object Type namespace.

- The exact user-visible string/number format is not decided here.
- The allocated value is frozen when the reservation is created.
- Renaming an Object Type must not rewrite existing Object IDs.
- An Object ID must not be reassigned to a different Object, even after Abandoned, Object deletion, Map deletion or project cleanup that preserves identity history.
- If the domain later needs a different type identity, use a new Object identity and a new reservation; do not mutate the existing identity's allocation namespace in place.

### 3.3 Project-wide namespace recommendation

The allocator should enforce uniqueness for `(Project, Object Type, allocated ID)`.

Rationale:
- Objects may be Shared across Maps.
- The same Object identity must resolve consistently wherever referenced.
- Map-local allocation would allow duplicate IDs for distinct objects of the same type in different Maps, creating ambiguity for cross-Map references and shared identity.
- Branches inherit Object identity; Branch is not an allocation namespace.

This is not a global cross-project uniqueness requirement. Different Projects may allocate the same Object Type-local ID independently.

## 4. Reservation lifecycle

| State | Meaning | Terminal? | Allowed next state |
|---|---|---:|---|
| `Reserved` | ID allocated and permanently consumed; not yet bound to an Object identity | No | `Bound`, `Abandoned` |
| `Bound` | Reservation bound to one specific formal Object identity/draft identity | No | `Inserted`, `Abandoned` |
| `Inserted` | Object creation has been committed and the reservation is consumed by that Object | Yes | None |
| `Abandoned` | Reservation will never produce an inserted Object; ID remains permanently consumed | Yes | None |

Rules:

- `Reserved → Bound` is one-way.
- `Bound` cannot be returned to `Reserved` or rebound to another Object.
- `Inserted` and `Abandoned` are terminal.
- An `Abandoned` reservation remains in durable identity-allocation history; it is not deleted or recycled.
- An `Inserted` reservation is the provenance record for the allocated Object ID.
- A reservation may be abandoned when its originating Candidate/Task is rejected or cancelled, or when its bound draft is permanently discarded.
- A temporary failure does not automatically mean Abandoned. Recovery must first determine whether the reservation or Object insertion committed.

## 5. Allocation protocol

Recommended Core Domain operation:

`ReserveObjectId(project, objectType, allocationRequestContext) → ObjectReservationRef + ObjectId`

The exact API shape is not prescribed.

The operation must:

1. Validate that the Object Type exists and is allocatable in the Project.
2. Read and advance the allocator state atomically.
3. Select the next never-used ID in that Project + Object Type namespace.
4. Persist a reservation in `Reserved` state.
5. Commit allocator advancement and reservation together.
6. Return the immutable Object ID and opaque reservation reference.

No ID may be returned to the allocator if any later step fails. If the reservation transaction itself rolls back before commit, the allocation was not made and the uncommitted value may be selected by a later successful reservation; no caller may have observed it as a valid reservation.

The allocator must not use `MAX(existing Object IDs) + 1` as its only source of truth, because abandoned reservations and gaps must remain consumed. It must consult durable allocation state that includes all reservations.

## 6. Binding and Object creation protocol

### 6.1 Binding

`BindReservation(reservationRef, objectIdentityRef)` is a Core Domain operation.

- Only a `Reserved` reservation can be bound.
- The binding is to exactly one stable Object identity/draft identity.
- The Object Type must match the reservation's Object Type.
- The Project must match the reservation's Project.
- A reservation cannot be bound to an Object in another Project or under another Object Type.
- A failed or discarded bound draft transitions to `Abandoned`; the reservation is not released.

### 6.2 Insertion

The final Object creation commit must atomically:

1. verify the reservation is `Bound` to the exact Object identity;
2. validate Project and Object Type consistency;
3. persist the formal Object identity/registration and its required initial creation data;
4. transition the reservation to `Inserted`;
5. commit once.

No visible formal Object may exist with an uninserted, mismatched or missing reservation where the domain requires permanent ID allocation.

If Object insertion fails, the formal Object and `Inserted` transition must both roll back. The reservation remains `Bound` if the transaction did not commit; the caller may retry the same insertion or explicitly abandon it. It must not silently allocate a replacement ID.

## 7. Relationship to Command, Candidate and Task

- A Candidate may request that an Object be created, but it does not own a formal Object ID.
- The AI Assistant may retain an opaque `ObjectReservationRef` and the returned allocated ID as proposal evidence.
- Candidate rejection or Task rollback does not reverse the reservation.
- A reservation can be created before formal Command submission; it does not itself create an Object or mutate World State.
- The formal Object creation Command/proposal must reference the exact reservation.
- The Core Domain validates reservation state and binding at preflight and again at final submission.
- The Object creation and transition to `Inserted` must be atomic with the formal history/application transaction.
- A lost response after commit must be reconciled using the stable reservation and Object identity references; do not allocate another ID blindly.

## 8. Deletion, Branching and Shared Object behavior

- Deleting an Object from a Map's runtime history does not free its Object ID.
- An Object ID remains consumed even if the Object is deleted in every Map that referenced it.
- A Branch does not allocate a new ID for an inherited Object.
- Branch-local changes to an inherited Object retain the same Object identity.
- Copy Object is a new semantic Object identity and therefore requires a new reservation, even if its geometry/attributes initially copy another Object.
- Geometry split/merge alone does not allocate or consume Object IDs unless a separate Object Operation explicitly creates new semantic Objects.
- A Shared Object has one identity and one reservation provenance, even when referenced by many Maps.
- Map-local Event-driven runtime changes do not create new Object identities unless the Event's explicit domain operation creates a new Object.

## 9. Failure, cancellation and recovery

| Situation | Required behavior |
|---|---|
| Reservation transaction fails before commit | No valid reservation exists; allocator advancement rolls back with it |
| Reservation committed, caller loses response | Query/reconcile by stable request/reservation identity; do not allocate blindly |
| Candidate rejected after reservation | Transition reservation to `Abandoned` if no longer needed; ID remains consumed |
| Task cancelled before binding | Reservation may transition `Reserved → Abandoned`; never recycle |
| Bound draft permanently discarded | Transition `Bound → Abandoned`; never rebind |
| Task Undo runs | Undo AI staging only; reservation consumption remains |
| Object insertion transaction fails | Object insertion and `Inserted` transition both roll back; reservation remains `Bound` |
| Object insertion commit succeeds but response is lost | Reconcile exact reservation/Object; do not duplicate Object or allocate another ID |
| Process crashes | Startup recovery inspects durable reservation state and related Object identity; no state inferred from AI logs alone |

A reservation must not be abandoned solely because a Task is marked Failed if the formal insertion may already have committed. Reconcile first.

## 10. Persistence and integrity requirements

The physical design must support these invariants:

1. Uniqueness of allocated ID within Project + Object Type.
2. Uniqueness of reservation reference.
3. At most one Object identity bound to a reservation.
4. At most one reservation provenance for a formal Object identity.
5. Object Type and Project match across reservation, binding and inserted Object.
6. Legal state transitions only; no terminal-state reopening.
7. `Abandoned` and `Inserted` reservations are retained permanently for the lifetime of the Project's identity history.
8. Allocator state cannot move backwards or reuse an abandoned allocation.
9. Object insertion and `Bound → Inserted` are one atomic database transaction.
10. All changes occur through Core Domain Domain Services; AI Assistant has no direct write permission.

The database may use constraints/triggers as defense in depth, but lifecycle transitions remain enforced by Core Domain services. Exact tables, columns, integer widths, sequence representation and triggers are deferred to integrated DDL design.

## 11. Minimum verification scenarios

1. Two reservations for the same Project + Object Type receive distinct IDs.
2. Two Maps in one Project cannot receive duplicate IDs for the same Object Type namespace.
3. Different Projects may independently allocate the same local ID if the namespace rules allow it.
4. A reservation for one Object Type cannot bind to another Object Type.
5. A reservation from one Project cannot bind to an Object in another Project.
6. `Reserved → Bound → Inserted` succeeds exactly once.
7. `Reserved → Abandoned` consumes the ID permanently.
8. `Bound → Abandoned` consumes the ID permanently and prevents rebinding.
9. `Inserted` and `Abandoned` cannot transition again.
10. Task rollback does not reclaim the ID.
11. Deleting an Object does not reclaim the ID.
12. Branch inheritance does not allocate a new ID for inherited Objects.
13. Copy Object allocates a new ID.
14. Geometry-only operations do not allocate IDs.
15. Object creation and `Inserted` state commit atomically.
16. A failed insertion leaves no formal Object and does not lose the bound reservation.
17. A lost response after reservation or insertion is reconciled without duplicate allocation/insertion.
18. Allocator recovery after process restart does not reuse any committed reservation, including Abandoned reservations.

## 12. Open items before physical DDL

1. Confirm whether the Object Type allocation namespace key is represented by a stable Object Type ID or a separate immutable allocation namespace ID.
2. Define the exact Object ID display/serialization format and whether IDs are numeric, textual or composite.
3. Define the reservation request idempotency key used to reconcile a lost response.
4. Define whether Object identity is created at `Bound` time or only materialized at `Inserted` time; the logical reservation remains distinct either way.
5. Map reservation and allocator state to actual Core Domain keys and SQLite constraints.

## 13. Status

This document establishes the recommended logical contract: **Object IDs are allocated by Core Domain per Project + Object Type, reserved permanently, bound once, inserted atomically, and never recycled.**

The four lifecycle states and permanent-consumption rule are retained. The exact namespace/display representation and physical persistence remain open for P2-09 DDL. This is not executable schema or implementation authorization.