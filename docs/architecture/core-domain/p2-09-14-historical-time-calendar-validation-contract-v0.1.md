# World View P2-09-14 — Historical Time and Calendar Validation Contract V0.1

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended validation contract and acceptance-test baseline — not a frozen ADR, executable DDL, or implementation authorization
- Depends on: P2-01-D01 D01-11~28 and D01-37~49; P2-09-05 V0.3; P2-09-11 V0.8; P2-09-16 V0.1; P2-09-12 V0.2
- Scope: Canonical Historical Time normalization, Calendar interpretation/configuration, precision intervals, sustained Command timing, Branch-local Calendar inheritance, and Checkpoint isolation

## 1. Purpose and authority

This contract defines the validation evidence required before the Historical Time and Calendar persistence design can be considered ready for executable DDL.

Core Domain is the sole authority for:
- Map-relative canonical Historical Time;
- conversion of author-entered Calendar dates into canonical time intervals;
- Calendar identity and time-effective display configuration;
- Command precision and sustained execution-time semantics;
- Branch-local Calendar configuration inheritance;
- validation of explicit ordering constraints and time ambiguity.

AI Assistant may propose dates, precision, durations, and ordering intent. It must use Core Domain conversion and validation results. It must not independently convert Calendar dates into a competing canonical time axis or silently resolve ambiguous ordering.

This document validates the recommended semantics in P2-09-05 V0.3. It does not promote D01-11~28 or D01-37~49 from Proposed / Discussion to Accepted/Frozen status.

## 2. Canonical Historical Time acceptance baseline

The canonical coordinate is Map-relative:
- `day_number` is a non-negative integer;
- `offset_seconds` is an integer in the range `[0, 86399]`;
- boundaries are compared lexicographically by `(day_number, offset_seconds)`;
- day zero is independently defined by each Map;
- Branches inherit the exact parent time axis;
- transaction timestamps and UTC wall-clock values are never substitutes for Historical Time;
- V1 precision is one second; sub-second precision requires a separately versioned extension.

Every author-entered time precision is normalized once to a half-open interval `[start, end)`:
- second precision covers one second;
- minute precision covers the represented minute;
- day precision covers the represented Calendar day;
- month/year precision covers the full represented period under the selected Calendar rules;
- `end` must be strictly greater than `start`.

Persist both canonical boundaries. A display timestamp or precision label alone is insufficient.

## 3. Calendar conversion and provenance

### 3.1 Conversion contract

For an author input, Core Domain must:
1. resolve the target Map and its applicable Calendar identity;
2. resolve the Calendar Configuration applicable to the entered Calendar period and selected History path;
3. validate the date components against that configuration's rules;
4. convert the input and its declared precision to canonical Map-relative `[start, end)` boundaries;
5. return the normalized interval and the Calendar provenance used for interpretation;
6. persist the normalized interval as the ordering authority.

Conversion must be deterministic. The same input, Map time axis, Calendar identity and effective Calendar rules must produce the same canonical interval.

After persistence, later Calendar reform or display-format changes must not reinterpret, move, or renormalize the stored Command/Event interval. `time_calendar_id` records the Calendar used for interpretation where applicable; `time_input_json` may preserve original input/display evidence. These fields do not override the normalized interval.

The final DDL review must decide whether retaining the exact source Calendar Configuration identity is needed as additional audit evidence. It must not introduce a replayable Calendar history or make the Calendar Configuration a second World State authority.

### 3.2 Required conversion fixtures

Validation must cover at least:
- ordinary Gregorian-like month/year rules;
- custom month counts and non-uniform month lengths;
- configured leap-day or leap-period rules;
- non-Gregorian era origin and Map day-zero offsets;
- first and last valid second of a day, month, and year;
- transitions across month/year boundaries;
- invalid dates under the active configuration;
- Calendar reforms that change rules before and after an effective boundary;
- auxiliary Calendar display conversion without changing canonical time.

The exact fixture definitions and expected outputs must be version-controlled with the validation tests. Do not use the host operating system's local time zone or Gregorian conversion as an implicit fallback.

## 4. Precision interval validation

For intervals A and B:
- A is definitely before B when `A.end <= B.start`;
- B is definitely before A when `B.end <= A.start`;
- overlapping intervals are not ordered by precision label, lower boundary, insertion order, or transaction timestamp.

When intervals overlap:
1. apply existing explicit and valid dependency/order constraints;
2. derive deterministic implicit dependencies from Command semantics;
3. if multiple possible orders remain and can produce different World States, pause the affected Commands and request user ordering;
4. use a technical tie-break only when the resulting World State is invariant across the possible orders.

Acceptance cases must include equal second-precision instants, overlapping minute/day ranges, adjacent non-overlapping intervals, and a coarse year/month interval overlapping a precise instant.

## 5. Calendar Configuration validation

### 5.1 Scope and effective intervals

- Each Map has one default Calendar and may expose auxiliary Calendars.
- Calendar Configuration is scoped to a Core Domain History and effective over a canonical Historical Time interval.
- Branch-owned configurations are local to the owning Branch History and participate only through the selected Branch inheritance path.
- A Branch-local configuration must not alter its parent History, sibling Branches, or unrelated Maps.
- Calendar Configuration is display/interpretation data, not a World State object or a replayable Command history.

### 5.2 Reform Command coupling

A Command-managed Calendar reform Configuration:
- has `configuration_origin='command_managed'`;
- references its source Command in the same History;
- is created/updated/deleted through the same Core Domain authoring transaction as its source reform Command;
- is retimed when the source Command is retimed;
- is removed when its source Command is deleted, with adjacent effective intervals recalculated;
- must not delete or rewrite independent `user_created` Configurations.

Do not implement this coupling as an implicit database cascade that bypasses Domain Services.

### 5.3 Overlap, gap, and fallback

Configurations for the same Calendar and History scope must not overlap.

A gap must not be silently interpreted using an arbitrary neighboring Configuration. The final Calendar contract must define whether a gap resolves to an explicit default rule or is invalid. Until that rule is confirmed and covered by fixtures, Core Domain validation must fail closed for an input whose applicable rules cannot be resolved unambiguously.

The validation suite must test:
- adjacent intervals with a shared boundary;
- overlapping intervals;
- an open-ended final interval;
- an intentional gap;
- insertion, deletion, and retiming of a reform Configuration;
- coexistence of user-created and command-managed Configurations;
- Branch-local changes that shadow only the selected Branch path.

## 6. Sustained Command timing validation

A sustained Command has:
- a canonical start precision interval;
- exactly one authoritative end representation: explicit end Historical Time or descriptor-defined duration;
- a descriptor-defined execution interval convention;
- optional pause/resume schedule facts where the Command supports pausing;
- zero or more runtime-derived State Changes.

Do not persist two independently editable authoritative end representations.

Pause semantics:
- World Historical Time continues to advance while a sustained Command is paused.
- The Command's execution progress does not advance during the pause.
- Resume continues from the previous execution progress.
- Expected completion moves later to account for the pause.
- An explicitly supplied end time is not silently rewritten; the user may edit it.

Tests must cover pause/resume, repeated pauses, resume at a later World Time, explicit-end versus duration exclusivity, and deterministic regeneration of stage State Changes.

## 7. Branch and Checkpoint isolation

- Branches inherit Calendar context through the selected History path and exact fork boundary.
- Branch-owned Calendar Configuration is visible only on that Branch and its descendants according to the established inheritance rule.
- A Calendar Configuration edit changes date interpretation only. It must not alter canonical Command/Event intervals, explicit ordering, Event/Command execution order, or World State.
- Calendar-only edits must not invalidate World State Checkpoints or trigger World State reconstruction.
- Checkpoint fingerprints must include reconstruction-affecting inputs, but exclude Calendar Configuration and narration-only data.

Shared Object current-definition edits, Map Initial State edits, applicable Commands/Event Effects, ordering constraints, exact descriptor/handler semantics, and reconstruction-algorithm compatibility remain independent Checkpoint inputs under P2-09-16.

## 8. Acceptance matrix

| ID | Scenario | Expected result |
|---|---|---|
| HT-01 | Same Calendar input and same effective rules converted twice | Identical canonical interval |
| HT-02 | Calendar rules changed after a Command was saved | Stored canonical interval remains unchanged |
| HT-03 | Invalid date under active Calendar rules | Validation error; no Command persisted |
| HT-04 | Month/year precision conversion | Full half-open period interval persisted |
| HT-05 | Adjacent precision intervals | Correct non-overlap ordering |
| HT-06 | Overlapping precision intervals with differing possible outcomes | Affected Commands paused for user ordering |
| CAL-01 | Reform Command created | Command and command-managed Configuration commit together |
| CAL-02 | Reform Command retimed/deleted | Its Configuration and adjacent intervals update atomically |
| CAL-03 | Independent user-created Configuration exists | Reform deletion does not remove it |
| CAL-04 | Two Configurations overlap | Validation rejects the resulting configuration set |
| CAL-05 | Configuration gap has no deterministic fallback | Fail closed; do not guess |
| CAL-06 | Branch-local Configuration changed | Parent and sibling display remain unchanged |
| CAL-07 | Calendar Configuration changed | No World State reconstruction or Checkpoint invalidation |
| DUR-01 | Sustained Command paused/resumed | World Time advances; Command execution clock pauses and resumes deterministically |
| DUR-02 | Explicit end and duration both supplied as authoritative | Validation rejects ambiguous dual authority |
| DUR-03 | Same schedule replayed with same handler semantics | Identical runtime stage State Changes |

## 9. Remaining design gates

1. Complete representative custom/non-Gregorian Calendar fixtures and independently verify expected canonical intervals.
2. Decide and document Calendar Configuration gap/default fallback behavior.
3. Confirm whether the exact source Calendar Configuration identity is retained as audit evidence in addition to `time_calendar_id` and original input evidence.
4. Validate Branch-specific Calendar Configuration inheritance against the complete D01 examples.
5. Confirm sustained Command end/duration representation for each concrete Command descriptor.
6. Validate all constraints, indexes and triggers against the selected SQLite provider and packaged Windows/macOS runtime.

## 10. Status

P2-09-14 V0.1 is a newly authored validation contract derived from P2-09-05 V0.3 and the current Core Domain persistence baseline. It makes validation requirements explicit without pretending unresolved Calendar semantics are settled.

It is not executable DDL, a provider test report, an Accepted/Frozen ADR, or implementation authorization. D01-11~28 and D01-37~49 remain Proposed / Discussion overall. Calendar conversion, gap/fallback behavior and Branch-specific Calendar inheritance remain open until the required fixtures and verification evidence exist.
