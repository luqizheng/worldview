# World View P2-09-14 — Historical Time and Calendar Validation Contract V0.2

- Version: 0.2
- Date: 2026-11-21
- Status: Recommended validation contract with clarified fail-closed rules; not a frozen ADR, executable DDL, or implementation authorization
- Supersedes: P2-09-14 V0.1; V0.1 retained as history
- Depends on: P2-09-05 V0.3; P2-09-11 V0.10; P2-09-16 V0.1; P2-09-12 V0.4
- Scope: canonical Historical Time normalization, Calendar interpretation/configuration, precision intervals, sustained Command timing, Branch-local Calendar inheritance and Checkpoint isolation

## 1. Purpose and authority

This contract defines validation evidence required before Historical Time and Calendar persistence can be considered ready for executable DDL. Core Domain alone owns Map-relative time, Calendar conversion, configuration applicability and ambiguity validation. AI Assistant may propose inputs but cannot independently convert or resolve ambiguity.

This revision clarifies fail-closed behavior and source-configuration provenance. It does not promote D01-11~28 or D01-37~49 from Proposed / Discussion to Accepted/Frozen.

## 2. Canonical time

- Canonical time is Map-relative: non-negative integer `day_number` and integer `offset_seconds` in [0, 86399].
- Boundaries compare lexicographically; day zero is Map-specific; Branches inherit the exact parent axis.
- V1 precision is one second. Sub-second precision requires a separately versioned extension.
- Author input normalizes once to half-open interval [start,end). Persist both boundaries.
- Transaction timestamps, UTC values and host local time are never Historical Time.

## 3. Calendar conversion and provenance

Core Domain resolves the target Map, selected History path, Calendar identity and exact effective Calendar Configuration before validating and converting input. The result includes canonical interval and provenance.

**Provenance rule:** persist the exact source Calendar Configuration identity (or immutable configuration fingerprint where identity is unavailable) used for conversion, in addition to Calendar identity and original input evidence. This is audit evidence only; it is not a replayable Calendar history and does not alter canonical time authority.

After persistence, later Calendar changes never reinterpret, move or renormalize stored Command/Event intervals.

## 4. Precision interval ordering

For A and B, A is definitely before B iff A.end <= B.start; B is definitely before A iff B.end <= A.start. Overlapping intervals are not ordered by insertion order, precision label, transaction time or arbitrary tie-break.

Apply explicit valid constraints and deterministic semantic dependencies. If multiple possible orders remain and can produce different World States, pause affected operations and request author ordering. A technical tie-break is allowed only if all valid orders produce the same World State.

## 5. Calendar Configuration scope and gaps

- Each Map has one default Calendar and may expose auxiliary Calendars.
- Configuration belongs to a Core Domain History scope and is visible only along the selected History inheritance path.
- Configurations for the same Calendar and same History scope must not overlap.
- **A gap is invalid for conversion.** No implicit previous/next/default-neighbor fallback is allowed. The author must repair the configuration interval or explicitly define a configuration covering the requested period.
- An open-ended final interval is permitted only when explicitly represented and unique for that History scope.
- A default Calendar is an explicit baseline Configuration covering the Map's supported time domain; it is not a hidden fallback for gaps in later reform intervals.

## 6. Branch inheritance

A Branch inherits the parent's applicable Calendar Configuration path through the exact fork boundary. The same closed-boundary inheritance rule applies: parent configurations effective at or before `parent_at` are inherited; parent configurations after `parent_at` are not visible.

Branch-owned Calendar Configuration:
- is local to that Branch and its descendants;
- must begin strictly after `parent_at`;
- shadows/replaces inherited interpretation only over its explicit effective interval;
- cannot create an overlap within the selected effective configuration set;
- never changes parent or sibling Branches.

Where a Branch-local configuration creates a gap in its effective path, conversion fails closed. The Branch must explicitly provide continuous coverage for the period in which its Calendar is expected to be used. A Branch cannot retroactively rewrite the inherited prefix at or before `parent_at`.

## 7. Reform Command coupling

A command-managed reform Configuration and its source reform Command are created, retimed and deleted in one Core Domain authoring transaction. Deleting/retiming it must not remove or rewrite independent user-created Configurations. Domain Services own interval recalculation; no database cascade may bypass them.

## 8. Sustained Commands

A sustained Command has a canonical start interval and exactly one authoritative end representation: explicit end time or descriptor-defined duration. Pause/resume affects execution progress, not the passage of World Historical Time. Explicit end values are never silently rewritten.

## 9. Checkpoint isolation

Calendar-only edits affect date interpretation, not canonical Command/Event intervals, ordering or World State. They do not invalidate World State Checkpoints or trigger reconstruction. Reconstruction fingerprints include only inputs that affect reconstructed World State.

## 10. Acceptance matrix additions

| ID | Scenario | Expected result |
|---|---|---|
| CAL-08 | Conversion input falls in a configuration gap | Fail closed; no neighboring/default configuration is guessed |
| CAL-09 | Conversion is repeated after source configuration changes | Previously stored canonical interval remains unchanged; provenance points to original source configuration |
| CAL-10 | Branch-local configuration starts at or before parent_at | Reject |
| CAL-11 | Branch inherits parent configuration through fork boundary | Configurations at/before boundary visible; later parent configurations invisible |
| CAL-12 | Branch-local config shadows part of inherited future | Only that Branch path changes; no parent/sibling mutation |
| CAL-13 | Branch effective configuration path contains a gap | Fail closed until explicit coverage is restored |

## 11. Remaining gates

1. Build independently checked custom/non-Gregorian fixtures and expected canonical intervals.
2. Verify the closed-boundary inheritance rule against every D01 Branch example.
3. Verify interval continuity and shadowing algorithm for multiple reforms and nested Branches.
4. Validate sustained Command end/duration and pause/resume semantics per concrete descriptor.
5. Validate constraints/indexes and transaction behavior against the packaged Windows/macOS SQLite provider.
6. Confirm exact configuration identity/fingerprint storage representation in the physical schema.

## 12. Status

V0.2 clarifies recommended validation behavior; it remains a proposal/test contract. Calendar conversion fixtures, nested Branch inheritance and SQLite provider evidence are not claimed complete.