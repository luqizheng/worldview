# World View Core Domain — P2-09-05 Historical Time, Calendar and Ordering Persistence Contract V0.3

- Date: 2026-11-21
- Status: Recommended V1 canonical-time, precision-range and Calendar persistence baseline; D01 remains Proposed / Discussion and this is not a frozen ADR
- Supersedes: P2-09-05 V0.2 for the Calendar persistence detail; canonical time and ordering rules are retained
- Depends on: P2-01-D01 D01-11~28 and D01-37~49; ADR-001/002/003/005; P2-09-02/03 V0.2

## 1. Authority

Core Domain alone owns canonical Historical Time, Calendar interpretation, Command time/duration semantics, explicit ordering constraints, dependency evaluation, conflict/cycle detection and deterministic reconstruction order.

AI Assistant may propose time and ordering intent. It must not maintain a parallel authoritative ordering graph or silently rewrite a user's explicit relation.

## 2. Canonical internal time coordinate

- Each Map independently defines day 0, the first point of its timeline.
- Internal time is independent of Gregorian dates, month lengths, leap years and era names.
- A time boundary is represented as integer `day_number` plus integer `offset_seconds` within the day.
- `day_number >= 0`; `0 <= offset_seconds < 86400`.
- Compare boundaries lexicographically by `(day_number, offset_seconds)`.
- No floating-point time values.
- Branches inherit the exact parent time axis and day-zero definition.
- Transaction timestamps are never substituted for Historical Time or used as semantic ordering tie-breakers.
- Current V1 precision ceiling is one second; future sub-second precision requires an explicit versioned extension.

## 3. Declared precision as a canonical interval

D01-21/22 establish that each Command may select its own precision up to seconds, lower precision does not create priority, and insufficient precision within the same time range must be resolved through dependencies or user ordering.

Represent every author-entered time precision as a half-open internal interval `[start,end)`:

- `start` is the first internal second represented by the author's time input;
- `end` is the first internal second not represented by that input;
- `end` is strictly greater than `start`;
- the interval is expressed in the Map's canonical day/offset coordinate, not in display-calendar dates.

Examples:
- second precision: `[12:00:05, 12:00:06)`;
- minute precision: `[12:00:00, 12:01:00)`;
- day precision: `[day_start, next_day_start)`;
- year/month precision: from the first internal second of the selected Calendar period to the first internal second of the next period, after one-time conversion through that Calendar.

Persist both interval boundaries. Do not persist only a representative timestamp for coarse input.

### 3.1 Ordering rules for precision intervals

- If `A.end <= B.start`, A is temporally before B.
- If `B.end <= A.start`, B is temporally before A.
- If intervals overlap, do not order by precision label or lower boundary alone.
- First apply existing explicit and valid dependency/order constraints.
- If possible orders remain and they can produce different World States, pause affected Commands and request user ordering.
- A deterministic technical tie-breaker is allowed only when semantic results are invariant.
- Exact second-precision Commands at the same second have overlapping one-second intervals and therefore require the same dependency/explicit-order analysis.

The half-open convention is a recommended representation rule, not a new business priority rule. It makes adjacent precision ranges non-overlapping and avoids inclusive-end ambiguity.

### 3.2 Calendar provenance

- `time_calendar_id` records the Calendar used to interpret the original author input where applicable.
- Store the resolved internal interval at authoring time. A later Calendar reform/display change must not reinterpret or move the stored interval.
- `time_input_json` is optional original-input/display evidence only; normalized interval columns are the ordering authority.

## 4. Calendar model and persistence

D01-40~45 establish a display/interpretation subsystem separate from internal Historical Time and World State:

1. Each Map has one default Calendar and may display additional auxiliary Calendars.
2. Calendar rules are user-customizable in V1 at a basic level: days per year, month structure, epoch/era origin and simple leap rules. Complex astronomical cycles are deferred.
3. The default Calendar's configuration may be effective over historical time intervals.
4. A Calendar reform may be represented by both a historical Command and a separate Calendar Configuration. The Command records the historical action; the Configuration stores the actual rules and effective interval.
5. Deleting a Calendar reform Command deletes its corresponding Command-managed Configuration and recomputes the adjacent effective intervals, restoring the previous rules.
6. Retiming a reform Command updates the associated Configuration's effective interval and recomputes subsequent display intervals.
7. Independent user-created Calendar Configurations not activated by that Command remain unaffected.
8. Calendar reform changes date interpretation only. It does not change internal cumulative days, Command execution time, State Change execution time, event order or World State; it does not require World State reconstruction.

Recommended persistence model:

- `core_calendar` stores Calendar identity, owning Map and role (`default` or `auxiliary`). A partial unique index permits only one default Calendar per Map.
- `core_calendar_configuration` stores one configuration definition and its effective interval, scoped to a Core Domain History.
- A user-created configuration has `configuration_origin='user_created'` and no `source_command_id`.
- A Command-managed reform configuration has `configuration_origin='command_managed'` and references its source Command in the same History.
- A Branch-owned Calendar Configuration is local to that Branch History and is inherited only through the normal Branch inheritance path; it does not change the parent or sibling Branch display.
- Deleting/retiming a reform Command and updating/deleting its associated configuration occur through one Core Domain authoring transaction. Do not use an implicit database cascade that bypasses Domain Services.
- The effective interval is represented by start and optional exclusive end boundaries. Adjacent configurations must not overlap; gap/default fallback behavior is a Core Domain validation rule.

Calendar Configuration is not a World State object and does not create a second replay history. It affects only date formatting/interpretation.

## 5. Instantaneous and sustained Commands

### 5.1 Instantaneous Command

An instantaneous Command has one declared-precision interval. At second precision the interval covers exactly one second. At coarser precision it represents the full range of possible intended instants.

### 5.2 Sustained Command

A sustained Command has:
- a start precision interval;
- either an explicitly supplied end Historical Time or a descriptor-defined duration;
- a defined execution interval convention;
- optional pause/resume schedule facts where the Command supports pausing;
- zero or more runtime-derived State Changes.

Persist only one authoritative duration representation: explicit end pair or duration. Do not maintain two independently editable values.

### 5.3 Pause semantics

D01-49 distinguishes world time from a sustained Command's execution clock:
- World Historical Time continues to advance during a pause.
- The paused Command's execution progress does not advance.
- On resume, execution continues from prior progress.
- Expected completion moves later to account for the pause.
- The system does not silently rewrite an explicitly set end time; the user may edit it.

Persist enough descriptor-specific schedule information to deterministically derive active execution segments and State Changes. Do not introduce a generic timer/workflow subsystem.

## 6. Explicit ordering constraints

D01-20 selects saving user-specified order as an explicit relation between two Commands, not a numeric rank or one-time sorted sequence.

Persist stable relation identity, owning History, source and target Command identities, direction, relation kind (ordering or prerequisite/dependency), provenance and transaction metadata.

Keep three layers distinct:
1. Explicit user-authored constraints — authoritative persisted data.
2. Derived implicit dependencies — recalculated from Command semantics and current History context.
3. Deterministic technical tie-break order — used only when semantic results are invariant; not persisted as a user relation.

## 7. Conflicts, cycles and editing

- A user-authored relation may be saved even if it creates a cycle.
- Preserve cyclic/conflicting relations; derive current conflict status from the current Commands and relations.
- Pause Commands in the cycle and their dependent chain; unrelated Commands may receive independent diagnostics.
- Changing a Command's time does not silently remove or rewrite an explicit relation.
- Deleting a relation triggers dependency/order reevaluation; it does not guarantee ambiguity is resolved.
- Command insertion, deletion or time edits invalidate affected Checkpoints and trigger the established reconstruction lifecycle.
- Commands remain author-editable records; no Command Revision Chain is introduced.

## 8. Branch inheritance

- Parent History ordering constraints are inherited according to the closed fork boundary.
- A Branch may add Branch-owned constraints but cannot override or rewrite inherited constraints.
- A Branch-owned Command precision interval must begin strictly after `parent_at`; every runtime State Change it generates must also be later than that boundary.
- A Branch `parent_at` is an exact canonical boundary, not a coarse uncertainty interval.
- Branch-owned Calendar Configurations are local to that Branch History and do not change parent or sibling Branches.

## 9. Persistence consequences

Persist:
- `core_command` with normalized precision interval, optional Calendar provenance and sustained-command end/duration data;
- `core_event` with its own historical precision interval;
- `core_command_order_constraint` with explicit relation endpoints and provenance;
- `core_calendar` and `core_calendar_configuration` for Calendar identity and time-effective display rules.

Do not persist derived implicit dependencies or runtime State Changes as authoritative tables in V1.

Checkpoint fingerprints include selected History lineage, Commands, Events, explicit ordering constraints, descriptor semantics, Map Initial State and current Shared Object definitions. They exclude Calendar Configuration because calendar-only changes do not alter World State reconstruction.

## 10. Status and remaining review

V0.3 recommends half-open precision intervals and a minimal Calendar identity/configuration model based on D01-40~45. It preserves the rule that Calendar changes do not alter internal Historical Time or require World State reconstruction.

The exact conversion of non-Gregorian Calendar periods into internal day/offset boundaries, Calendar configuration overlap/gap behavior and Branch-specific Calendar display inheritance must be validated against the complete Calendar descriptors before final DDL. D01-11~28 and D01-37~49 remain Proposed / Discussion records overall; this contract is a recommended design working baseline, not a claim that those records have become Accepted/Frozen ADRs.