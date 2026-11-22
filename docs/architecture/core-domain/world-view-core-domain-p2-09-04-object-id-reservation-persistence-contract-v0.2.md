# World View Core Domain — P2-09-04 Object ID Reservation Persistence Contract V0.2

- Date: 2026-11-21
- Status: Recommended V1 allocation and persistence baseline — exact display grammar remains open
- Supersedes: P2-09-04 V0.1 for the physical identity/binding recommendation; the four lifecycle states and permanent-consumption rules are unchanged
- Depends on: P2-09-01/02; established Object ID Reservation lifecycle decision; P2-09-11 V0.4

## 1. Core rules retained

1. Core Domain exclusively allocates formal Object IDs. AI Assistant, Candidate generation, plugins and UI code cannot fabricate or recycle them.
2. Object Type controls the allocation namespace.
3. Reservation lifecycle is `Reserved → Bound → Inserted`, with `Abandoned` as a terminal outcome reachable from `Reserved` or `Bound`.
4. Once a reservation commits, its allocated ID is permanently consumed. Abandoned IDs are never reused.
5. Task failure, cancellation, Candidate rejection, Task Undo and application retry never reverse a committed reservation.
6. A reservation is an allocation fact, not itself a formal Object insertion.

## 2. Recommended namespace scope

Use **Project + stable Object Type identity** as the V1 allocation namespace.

- A Project is the local persistence unit and owns its Object Types.
- Shared Objects may be referenced by multiple Maps in the same Project, so Map-local allocation would risk duplicate Object Type-local IDs for distinct objects and would complicate cross-Map identity resolution.
- Branches inherit Object identity and are never an allocation namespace.
- Different Projects may independently allocate the same Object Type-local value; V1 does not require global cross-project uniqueness.

This is the recommended technical interpretation of “Object IDs are allocated by Object Type”. It does not assert that the user separately selected a global namespace policy.

## 3. Domain Object ID versus relational key

Use two distinct representations for different purposes:

- **Object Type-assigned Object ID value** (`allocated_id_value`): the permanent domain-facing identifier produced by the Object Type allocator and shown to the user. It is frozen at reservation time and never recycled.
- **Object identity storage key** (`object_identity_id`): a stable UUID used for relational references inside the database and APIs. It is not a second user-facing Object ID, does not replace the allocated value and does not create an Object Version concept.

The reservation retains both the allocated value and the bound identity key. Core Domain APIs should return both when an Object is bound/inserted so the UI and AI Assistant never infer one from the other.

## 4. Allocation representation

Recommended physical fields:

- `project_id`;
- stable `object_type_id`;
- monotonic `allocated_sequence`;
- immutable `allocated_id_value`;
- unique `reservation_id`;
- optional idempotent `request_key`;
- reservation state and timestamps.

Enforce uniqueness for `(project_id, object_type_id, allocated_sequence)` and `(project_id, object_type_id, allocated_id_value)`. The allocator state and reservation insertion commit together.

The exact display grammar (numeric, textual, prefixed or otherwise) remains an Object Type configuration contract. The stored `allocated_id_value` is text so formatting changes cannot rewrite previously allocated IDs. `allocated_sequence` is an internal monotonic allocation ordinal, not the display value itself.

## 5. Binding recommendation

At `Reserved → Bound`:

1. Core Domain creates a stable `object_identity_id` UUID.
2. Core Domain creates a non-public draft identity row in `core_object` with lifecycle `draft` and the matching Project/Object Type.
3. The reservation stores that exact `object_identity_id` and becomes `Bound` in the same transaction.
4. The draft identity is not yet an inserted world Object and must not appear in Map membership or Initial State until the formal Object creation commit.

This makes the reservation distinct from the draft Object identity while ensuring the binding is durable and cannot be rebound.

At `Bound → Inserted`:
- the Object identity row transitions from `draft` to `active`;
- the reservation transitions to `Inserted`;
- required definition/registration/creation history data is persisted;
- all changes commit in one transaction.

At `Bound → Abandoned`:
- retain the Object identity row as an `abandoned` tombstone;
- retain the reservation and allocated ID permanently;
- never rebind or recycle either identity.

At `Reserved → Abandoned`:
- no Object identity row is required because no identity was bound;
- retain the reservation and allocated ID permanently.

This draft-row recommendation is a persistence strategy, not a change to the domain lifecycle: `Inserted` remains the point at which the formal Object is created in the modeled world.

## 6. Retry and idempotency

- `request_key` is stable for one logical allocation request and unique within the Project.
- If the response is lost after reservation commit, Core Domain looks up the same reservation by request key and returns the same allocated value and reservation reference.
- A retry must never allocate a second ID merely because the caller did not receive the first response.
- If a bound draft is abandoned, a new logical Object creation requires a new reservation and a new Object identity; the abandoned ID remains consumed.

## 7. Required constraints

1. Unique allocation within Project + Object Type.
2. No allocator rollback after a committed reservation.
3. At most one Object identity bound to one reservation.
4. At most one reservation provenance per Object identity.
5. Project and Object Type match across reservation and identity.
6. Only `Reserved` may transition to `Bound`; only `Reserved` or `Bound` may transition to `Abandoned`; `Bound` may transition to `Inserted`; terminal states never reopen.
7. An Object identity row may be `draft`, `active`, `abandoned` or `retired`; it is not physically deleted while its reservation evidence is retained.
8. Formal Object insertion and `Bound → Inserted` are atomic.
9. Task Undo does not reverse any committed reservation or binding.
10. All writes occur through Core Domain Domain Services.

## 8. Remaining display/API detail

The exact user-visible Object ID grammar and Object Type allocation-format editing rules remain a separate Object Type design detail. Changing an Object Type display name or formatting policy must not rewrite existing `allocated_id_value` values or change their namespace identity.

## 9. Status

V0.2 recommends Project + Object Type allocation, a stable internal UUID storage key separate from the permanent Object Type-assigned value, and durable draft identity creation at Bound time. The four reservation states and non-recycling rule remain unchanged. This is a design baseline, not executable DDL or implementation authorization.