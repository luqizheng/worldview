# World View P2-09-14 — Historical Time and Calendar Validation Contract V0.1

- Date: 2026-11-21
- Status: Recommended V1 validation baseline and physical-schema gap review; not a frozen ADR, executable DDL, or implementation authorization
- Depends on: P2-09-05 V0.3; P2-09-11 V0.7; P2-09-13 V0.1; P2-01-D01 D01-37~49; ADR-001/002/003 Amendment V1.3/005

## 1. Purpose

Close the remaining design gap between the canonical Historical Time representation, user-entered Calendar expressions, time-effective Calendar Configurations, sustained Commands, and the relational schema proposal.

This contract preserves the central distinction:

- Historical Time is the Map-relative internal time axis and ordering authority.
- Calendar is a display and interpretation system for expressing points/periods on that axis.
- Calendar Configuration changes do not rewrite stored Historical Time, Command order, Event order, State Change execution, or World State.

D01-37~49 contain user-confirmed stage decisions but remain Proposed / Discussion as a document set. This document therefore records a recommended technical baseline, not a claim that D01 has become a frozen ADR.

## 2. Canonical time representation

Retain the P2-09-05 / P2-09-11 representation:

- `day_number INTEGER`, non-negative and Map-relative;
- `offset_seconds INTEGER`, in `[0, 86399]`;
- compare lexicographically by `(day_number, offset_seconds)`;
- no floating-point values;
- each Map defines its own `(0,0)` origin;
- every Branch inherits the exact parent time axis and cannot redefine day zero.

Persist transaction timestamps separately as UTC Unix milliseconds. They are never Historical Time and never semantic ordering tie-breakers.

### 2.1 Declared precision

Represent author-entered precision as a half-open interval `[start, end)` in canonical internal time. Persist both boundaries. The interval is the ordering authority; the precision label and original Calendar text are explanatory/input evidence only.

Examples:

| User precision | Canonical interval |
|---|---|
| Exact second at D 12:00:05 | `[D/43205, D/43206)` |
| Minute 12:00 | `[D/43200, D/43260)` |
| Whole day D | `[(D,0), (D+1,0))` |
| Calendar year/month | First internal second of that period through the first internal second of the next period, after deterministic conversion through the selected Calendar Configuration |

The exclusive end must be strictly greater than the start. Adjacent intervals may touch; they do not overlap.

### 2.2 Branch boundary validation

For a Branch-owned Command, the complete declared precision interval must start strictly after the exact `parent_at` boundary:

`command.precision_start > branch.parent_at`

Do not accept a coarse interval whose start is at or before `parent_at` merely because some portion of the interval lies later. Do not silently move or narrow the interval. The user must choose a later time, a more precise time, or another target History/fork.

The same boundary applies to any Branch-owned Calendar Configuration or other Branch-local time-effective authoring record: its effective start must be strictly later than `parent_at`. Inherited parent configurations at or before the fork boundary remain visible through the selected inheritance path; no configuration is copied at fork creation.

## 3. Calendar identity and configuration

### 3.1 Identity

- Each Map has exactly one default Calendar and may have zero or more auxiliary Calendars.
- Calendar identities belong to a Map; Branches inherit the Map's Calendar identities and time axis.
- Auxiliary Calendars provide alternate date expressions. They do not change the internal time axis or execution order.
- V1 Calendar rules are intentionally basic: days per year, month structure, epoch/era origin, and simple leap rules. Complex astronomical cycles are deferred.

### 3.2 Configuration intervals

Use half-open effective intervals `[effective_from, effective_until)`.

- A Map's default Calendar has a day-zero configuration beginning at `(0,0)`.
- A configuration with no end is open-ended within its owning History scope.
- Configurations for the same Calendar and effective History scope must not overlap.
- Adjacent configurations may meet exactly at one boundary.
- The effective configuration for a selected Branch is resolved along its single inherited History path: ancestor configurations visible at or before the closed fork boundary, followed by Branch-owned configurations strictly after that boundary.
- A Branch does not create a copied default configuration merely by being created.
- A Calendar reform Command and its command-managed Configuration are changed/deleted in one Core Domain authoring transaction. Deleting the reform removes only its linked command-managed Configuration; independent user-created configurations remain untouched.
- A configuration-only change refreshes date interpretation; it does not invalidate World State Checkpoints or trigger World State reconstruction.

The non-overlap rule is validated by Core Domain services and integrity checks. SQLite CHECK constraints cannot by themselves enforce arbitrary interval non-overlap across rows.

### 3.3 Precedence and gaps

Do not introduce a hidden priority between user-created and Command-managed configurations. Both participate in the same non-overlapping effective timeline for a given Calendar/History scope.

A gap in a non-default/auxiliary Calendar may be reported as “no configured date expression” and must not alter internal time. The default Calendar timeline must always resolve at every supported Historical Time: the Map day-zero configuration is open-ended until superseded by a valid later configuration, and each reform recomputes adjacent intervals. A default-calendar gap is an integrity error, not an invitation to guess a fallback Calendar.

## 4. Deterministic conversion of Calendar input

Calendar conversion is a one-time authoring operation:

1. Resolve the exact Calendar and effective Calendar Configuration for the selected History/time context.
2. Validate the input against that configuration's rules and schema version.
3. Convert the input to canonical `(day_number, offset_seconds)` boundary values.
4. For coarse precision, convert both the first instant of the selected period and the first instant of the next period to form `[start,end)`.
5. Persist the canonical interval as authoritative time.
6. Preserve optional original input evidence separately; later Calendar edits/reforms do not reinterpret the stored interval.

Invalid or ambiguous Calendar input is rejected for clarification. Conversion must not normalize an invalid date by silently carrying overflow into another month/year.

### 4.1 Calendar reform boundary ambiguity

If a coarse input period (for example, a month or year) crosses a Calendar Configuration boundary, the period's interpretation may change inside the represented interval. V1 should reject automatic conversion and request a more precise date/time or explicit user selection of the intended Calendar Configuration. Do not choose the configuration at the lower boundary and silently apply it to the entire coarse period.

### 4.2 Conversion provenance gap

P2-09-11 currently proposes `time_calendar_id` and optional `time_input_json`. Calendar identity alone does not identify the exact rules used when an input was converted, because a Calendar Configuration can later be edited or removed.

Recommended additive fields on Command/Event time input evidence:

- `time_calendar_configuration_fingerprint TEXT NULL` — deterministic digest of the exact Calendar Configuration rules used for conversion;
- retain `time_calendar_id`;
- retain `time_input_json` only when useful for author-facing explanation/audit.

This fingerprint is not a Calendar Version entity and does not become a second history. The already-normalized internal interval remains authoritative. The fingerprint only explains how the original human-readable date was interpreted.

## 5. Instantaneous and sustained Commands

### 5.1 Instantaneous Commands

An instantaneous Command has one declared-precision interval. At second precision it covers one internal second; at coarser precision it covers the full represented range.

### 5.2 Sustained Commands

A sustained Command has:

- a start precision interval;
- exactly one authoritative end representation: either an explicit end-time interval or a descriptor-defined duration;
- descriptor-defined execution interval semantics;
- optional pause/resume schedule facts only for Command types that support pausing;
- zero or more runtime-derived State Changes.

The physical proposal currently stores an optional explicit execution-end point pair or `duration_seconds`. That is insufficient if an explicit end time itself has declared precision. Recommended schema amendment: when explicit end-time precision is supported, store a complete end interval (end start pair plus exclusive end pair), not one representative point. If the descriptor only supports exact-second ends, state that restriction explicitly and validate it.

Do not store both an independently editable duration and explicit end interval as competing authorities. If duration is authoritative, derive the end interval deterministically from the start interval and duration according to the descriptor's defined anchor semantics.

### 5.3 Pause/resume

Preserve D01-49:

- World Historical Time continues during a pause.
- The sustained Command's own execution clock/progress pauses.
- Resume continues from the prior progress.
- Expected completion moves later to account for paused periods.
- The system does not silently rewrite a user-entered explicit end time.

Pause/resume schedule representation belongs to the specific sustained Command descriptor. Do not create a generic timer or workflow subsystem. The descriptor must define how pauses interact with an explicit end interval or duration; ambiguous combinations are rejected during preflight.

## 6. Calendar Configuration and Command lifecycle

- A Calendar reform Command is a historical authoring record; its linked Calendar Configuration is the display-rule effect.
- The association must be explicit and same-History scoped.
- Editing/retiming the reform updates the associated configuration and recomputes adjacent effective intervals in one Core Domain transaction.
- Deleting the reform removes its associated command-managed configuration and recomputes the prior/next effective intervals.
- Independent user-created configurations are not deleted as a side effect.
- Calendar-only changes never rebuild World State.
- The original Command/Event canonical Historical Time intervals are not recomputed when a Calendar Configuration changes.

## 7. Required verification matrix

| ID | Scenario | Expected result |
|---|---|---|
| HT-01 | Convert exact second input | One-second half-open interval |
| HT-02 | Convert minute/day/year/month precision | Correct full-period half-open interval |
| HT-03 | Invalid month/day/leap input | Reject; no overflow normalization |
| HT-04 | Coarse period crosses a Calendar reform | Request clarification/more precise input |
| HT-05 | Calendar Configuration edited after Command creation | Stored internal interval unchanged |
| HT-06 | Calendar Configuration fingerprint retained | Original input interpretation remains explainable |
| HT-07 | Two configurations overlap | Reject authoring transaction |
| HT-08 | Adjacent configurations meet at one boundary | Accept; no overlap |
| HT-09 | Default Calendar has a gap | Integrity failure; do not guess fallback |
| HT-10 | Auxiliary Calendar has a gap | Report no display mapping; internal time remains valid |
| HT-11 | Branch inherits parent configuration at fork boundary | Visible through inherited path; no copy |
| HT-12 | Branch-owned configuration begins at/before `parent_at` | Reject |
| HT-13 | Branch-owned configuration begins strictly after `parent_at` | Accept if non-overlapping on selected path |
| HT-14 | Delete Calendar reform Command | Delete only linked command-managed config and recompute intervals |
| HT-15 | Delete independent user-created configuration's unrelated Command | Do not delete the configuration automatically |
| HT-16 | Calendar reform retimed | Update config intervals; do not rebuild World State |
| HT-17 | Sustained Command with duration | Derive deterministic completion interval |
| HT-18 | Sustained Command with explicit end precision | Preserve end interval; do not collapse to one point |
| HT-19 | Pause/resume sustained Command | World time advances; command progress pauses and resumes deterministically |
| HT-20 | Branch Command precision interval crosses fork boundary | Reject; no silent shift |

## 8. P2-09-11 V0.7 physical-schema amendments

Before executable DDL, revise the relational proposal to:

1. Add optional `time_calendar_configuration_fingerprint` to `core_command` and `core_event` when original Calendar input provenance is retained.
2. Resolve explicit sustained-Command end precision: either add the end interval pair or explicitly restrict V1 descriptors to exact-second end times.
3. Add a service/integrity invariant that Branch-owned Calendar Configuration effective starts are strictly after `parent_at`.
4. Require the default Calendar to resolve continuously from day zero; gaps are invalid. Auxiliary Calendar gaps are permitted and produce no display mapping.
5. Make interval non-overlap and inheritance-path resolution explicit Core Domain service invariants; do not pretend a simple row CHECK/FK proves them.
6. Preserve Calendar Configuration fingerprint exclusion from World State reconstruction semantics: Calendar display changes do not alter the reconstructed world. The Checkpoint may retain the current Shared Object definition fingerprint, but not a Calendar Configuration fingerprint as a World State dependency.

## 9. Remaining design gates

- Validate the proposed conversion algorithm against representative custom Calendar fixtures, including leap rules, year/month boundaries, reform boundaries and Branch inheritance.
- Confirm whether V1 sustained Command descriptors need coarse-precision explicit end times; if not, document exact-second end-time restriction.
- Complete concrete V1 Command Type IDs and payload schemas; this document does not invent a speculative generic Command catalog.
- Keep D01 status as Proposed / Discussion until the project's formal D01 review/freeze process is completed.

## 10. Status

This contract closes the technical representation recommendations for canonical time, Calendar interval semantics and validation cases. It identifies two concrete physical-schema amendments (Calendar conversion fingerprint and sustained end-time precision) that must be reconciled into P2-09-11 before executable DDL. It does not claim that the Calendar implementation has been tested, that D01 is frozen, or that Schema V2.0 is authorized.