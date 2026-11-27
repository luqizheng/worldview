# FOUNDATION-01A — Default Spatial Reference Implementation v0.1

**Status:** Implementation in review on `foundation-01a-default-spatial-reference`.

## 1. Purpose

Every newly created Map receives an immutable, project-scoped coordinate-frame definition at creation time. The Map must never be persisted with an absent Spatial Reference binding.

This slice implements only the accepted coordinate-frame semantics. It does not choose numeric precision, authoring tolerances, or a finite valid extent.

## 2. Built-in definition

For each new Map, Core Domain creates a distinct immutable Spatial Reference definition:

| Property | Value |
|---|---|
| Kind | `LocalCartesian` |
| Dimension | `2` |
| Axis order | `XY` |
| Coordinate unit | `world-unit` |
| Origin | Map-local |
| Wrap behavior | `None` |
| Vertical reference | None |
| Valid domain | Unspecified; no finite extent is asserted |
| Precision profile | Unspecified |

The definition identity is project-scoped and derived from the newly allocated Map identity. A different Map receives a different definition identity, so Map-local coordinate frames cannot accidentally be shared.

## 3. Precision policy is a separate concern

A coordinate frame describes the coordinate space and axis/unit semantics. It does not need to contain an approved quantization or authoring precision policy.

The Spatial Reference model therefore permits either:
- a complete immutable precision profile; or
- no precision profile.

Partial profiles are invalid. SQLite enforces all-or-none persistence for the precision fields. A Field Revision that requires a precision profile is rejected while the owning Spatial Reference has no profile. No numeric values, units such as metres/feet, tolerances, or feature-size thresholds are fabricated.

The profile remains gated by DESIGN-EXTENT / PRA-03. Once that work is accepted, a separate immutable precision policy can be introduced through the appropriate design and migration.

## 4. Atomic creation boundary

`MapCreationPlan` carries both the Map record and its default Spatial Reference definition. The SQLite implementation performs the following within one transaction and the existing single-connection access coordinator:

1. Validate Map, project, and Spatial Reference ownership/identity consistency.
2. Check same-Project Map name uniqueness.
3. Insert the immutable Spatial Reference definition.
4. Insert the Map with its Spatial Reference foreign-key binding.
5. Insert root History, default Calendar, and day-zero Calendar Configuration.
6. Commit once.

Any failure rolls back the Spatial Reference and all Map structure rows. The definition is inserted before the Map only to satisfy the immediate SQLite foreign key; it is not independently committed.

The existing standalone definition-registration and initial-bind APIs remain available for explicitly authored definitions, but they cannot replace an already bound Map coordinate frame.

## 5. Persistence changes

- Precision profile columns in `core_spatial_reference` become nullable as one atomic group.
- The precision version check accepts NULL or a positive version.
- A table check constraint rejects partial precision profiles.
- The immutable definition fingerprint includes an explicit null precision-profile value.
- Existing complete precision profiles remain valid and retain their values/fingerprints.

The downgrade is intentionally rejected: converting an unconfigured coordinate frame back to a required precision profile would require inventing policy values. This project is unreleased and has no customer production data; the forward schema is the authoritative development path.

## 6. Verification

The implementation adds coverage for:
- built-in Local Cartesian definition values and unspecified precision;
- Map creation and readback of the default binding;
- close/reopen persistence of Map-to-definition binding;
- rollback of the inserted Spatial Reference when a later Map insert fails;
- existing explicit precision-profile validation and registration behavior.

CI and Windows/macOS Debug/Release results remain the acceptance evidence. This document does not claim the GUI authoring workflow is complete.
