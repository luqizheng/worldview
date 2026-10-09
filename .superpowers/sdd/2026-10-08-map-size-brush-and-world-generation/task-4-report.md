# Task 4 Report — CoastlineRefiner

## 1. Header

- **Parent commit (Task 3 HEAD)**: `ed17650`
- **Task 4 commits (on top of `ed17650`)**:
  - `bf8f441` feat(domain): 新增 CoastlineRefiner 纯 C# 细化 pass（去碎岛/填碎海/重标 coast）

(One commit only. An earlier commit `4ab54ee` accidentally swept in seven untracked `.uid` files left over from T2/T3 Godot imports plus three untracked `.md` spec/plan files; that commit was soft-reset and re-applied with `git add` limited to the two T4 deliverables, producing `bf8f441`. The original commit is no longer reachable from any branch.)

## 2. Files changed

`git diff --stat ed17650..HEAD`:

```
 scripts/Domain/Generation/CoastlineRefiner.cs      | 438 +++++++++++++++++++
 tests/WorldView.Tests/Generation/CoastlineRefinerTests.cs | 485 +++++++++++++++++++++
 2 files changed, 923 insertions(+)
```

- **Created** (2 files):
  - `scripts/Domain/Generation/CoastlineRefiner.cs` (+438)
  - `tests/WorldView.Tests/Generation/CoastlineRefinerTests.cs` (+485)
- **Modified**: none.

No other file was touched by Task 4. (Untracked items left over from T1–T3 — three `.md` files under `docs/superpowers/` and seven `.uid` sidecar files — were intentionally excluded from this commit and remain untracked; see Concerns §8.)

## 3. Interfaces delivered

Verbatim from `scripts/Domain/Generation/CoastlineRefiner.cs`:

```csharp
public sealed record RefineOptions(int MinIslandCells = 8, int MinLakeCells = 6, int Erosion = 0, bool Morphology = false);
```

```csharp
public sealed record RefineStats(int RemovedCells, int FilledCells, int RelabeledCells);
```

```csharp
public sealed record RefineOutcome(IReadOnlyDictionary<GridCoord, TerrainType> Changed, RefineStats Stats);
```

```csharp
public static RefineOutcome CoastlineRefiner.Refine(
    Func<int, int, TerrainType> read,
    int width, int height,
    GridCoord rangeMin, GridCoord rangeMax,
    TerrainType defaultType,
    RefineOptions options)
```

(`CoastlineRefiner` itself is `public static class CoastlineRefiner` in `namespace WorldView.Domain.Generation`.)

## 4. Algorithm note

**Connected-component labelling is implemented as iterative BFS (4-conn) using an explicit queue and a `bool[boxW, boxH]` visited grid.** The implementation deliberately does NOT use union-find because: (a) BFS over the dense view array is simple and matches the `FloodFill` mental model from the spec; (b) we need the per-component list anyway (for the boundary-touching check and the per-cell application of fills/deletes), which BFS hands back for free; (c) union-find would require either keeping a representative per cell (memory cost) or a second pass to enumerate the partition, with no asymptotic win since the scan box is small relative to the grid.

Two variants:
- `FloodFillLand4` — used for de-island. 4-neighbor (N/S/E/W), predicate `IsLand = (t != defaultType)`.
- `FloodFillWater4` — used for fill-lake. 4-neighbor, predicate `IsWater = (t == defaultType)`.

For the **coast relabel step** (step 4), the connectivity is 8-neighbor via a separate `HasWaterNeighbor8` helper that walks all 8 offsets, and which calls back to the in-box `view`. For cells on the box boundary (which by step 4 are still in the box because the box is `±1` outside the user's range), an additional `HasOuterWaterNeighbor8` issues `read()` calls *outside* the box to peek at the real water beyond the box edge — this is needed because otherwise box-edge cells would never see the water that lives at `(rangeMin.X - 1, …)` or beyond, and a 1-cell-wide coastline strip at the box's outer edge would be silently under-labeled. This dual-mode (in-box for interior, out-of-box read for box edges) keeps the 8-conn semantics correct while still respecting hard constraint C (we only read the immediate-outer 1-cell halo, not the whole grid).

Boundary-touching preservation: each component's cell list is checked with `Any(c => c.lx ∈ {0, boxW-1} || c.ly ∈ {0, boxH-1})` for the box edge, and the same with `gx(c.lx)/gy(c.ly)` mapped against the grid boundaries. Either condition skips the component. This implements the brief's "永不删除" rule for both de-island and fill-lake.

Erosion morphology (step 3, gated on `Morphology && Erosion > 0`) uses per-cell neighbor counting on a new `view` snapshot per round: erode = water wins if ≥3 of 4 neighbors are water; dilate = land wins if ≥3 of 4 neighbors are land. Erosion is conservative: when `Erosion = 0` (or `Morphology = false`) the step is skipped entirely (the `ErosionZero_DoesNotRunMorphology` test pins this).

## 5. Tests added

All 10 `[Fact]` methods below are in `tests/WorldView.Tests/Generation/CoastlineRefinerTests.cs`, namespace `WorldView.Tests.Generation`:

| # | `[Fact]` name | Asserts |
|---|---|---|
| 1 | `ThreeCellIsland_IsRemoved_AtThreshold8` | A 3-cell plain island (4-conn component) with `MinIslandCells=8` is reported as `defaultType` (deletion) for each cell, `Stats.RemovedCells == 3`, `Stats.FilledCells == 0`. |
| 2 | `BoundaryTouching_Component_Is_Preserved` | Hard A: a 3-cell land component touching both box and grid edges yields `Stats.RemovedCells == 0` and any `Changed` entry for those cells is NOT `defaultType` (they may be coast-relabeled, which is allowed). |
| 3 | `FourCellLake_IsFilled_AtThreshold6` | A 4-cell enclosed water body (4-conn) with `MinLakeCells=6` is filled to `Plain`, `Stats.FilledCells == 4`. Also asserts the `Changed.Count` invariant. |
| 4 | `FirstCellWideCanal_IsNotFilled` | Hard A + canal protection: a 1-cell-wide full-row water strip touching the left/right box boundary produces `Stats.FilledCells == 0` and no Changed entry on that row. |
| 5 | `CoastRing_IsRelabeled_IncludingExpandedRing` | Two plain cells just outside the user's range (but inside the `±1` box) with water 8-neighbors are relabeled to `Coast`; `Stats.RelabeledCells > 0`. (Removed the previous `(7,7)` assertion because `(7,7)` legitimately relabels to Coast when surrounded by water, which is correct behavior.) |
| 6 | `SecondPass_WithMorphologyOff_ChangesNothing` | Hard B: applying `first.Changed` and re-running `Refine` with `Morphology=false` produces `second.Changed` empty (idempotence). |
| 7 | `NothingOutsideRange_IsChanged` | Hard C: corner cells at `(0,0)`, `(19,19)`, `(0,19)`, `(19,0)` of a 20×20 grid are absent from `Changed` when the range is `(5,5)..(14,14)`. |
| 8 | `ErosionZero_DoesNotRunMorphology` | With `Morphology=true` but `Erosion=0`, the morphology step is skipped: `Stats.RemovedCells` exactly equals the 3-cell island area (no over-erasure), and a second pass is empty. |
| 9 | `ReturnedDeletion_IsReportedAsDefaultType` | After `Refine`, calling `Apply` with the `Changed` dict (removing entries whose new value equals `defaultType`) produces a sparse dictionary with the 3 island cells gone, not written as `Water`. |
| 10 | `StatsCounts_MatchChangedSize` | The invariant `Stats.RemovedCells + Stats.FilledCells + Stats.RelabeledCells == Changed.Count` holds for a multi-feature layout (3-cell island + 4-cell lake + coast cells). |

### PASS/FAIL counts

- **(a) New test class `WorldView.Tests.Generation.CoastlineRefinerTests` only**: `Total: 10, Errors: 0, Failed: 0, Skipped: 0`. **All 10 pass.**
- **(b) Full xunit console run from `examples\sample_world` cwd**:
  ```
  Total: 131, Errors: 0, Failed: 7, Skipped: 0
  ```
  **Exactly 7 failures**, matching the pre-T4 baseline (T3 ended at Total 121, Failed 7; T4 added 10 new facts → 131). The 7 failing names are the same as the ledger-pinned baseline:
  - `BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`
  - `BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`
  - `ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`
  - `YamlSchemasTest.Deserialize_GridCoord_As_Array`
  - `UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`
  - `UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`
  - `ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`

  No regression. **Stop condition NOT triggered.**

## 6. TDD evidence

| Phase | Evidence |
|---|---|
| **RED** | Before the `Refine` body was written, the `CoastlineRefiner.Refine` symbol did not exist; `dotnet build` of `WorldView.Tests.csproj` failed with `error CS0103: The name 'CoastlineRefiner' does not exist in the current context`. Test compilation = first RED. (I did not commit an interim state to keep the log clean, but the build error was the gate.) |
| **GREEN** | After writing `Refine` plus the four flood-fill / neighbor-counting helpers, full `CoastlineRefinerTests` run reports `Total: 10, Failed: 0`. |
| **REGRESSION** | Full suite from `examples\sample_world`: `Total: 131, Failed: 7` — exactly the baseline. No new failures, no errors, no new skips. |

## 7. YAGNI / scope deviations

- **Outward boundary dilation of the box for the coast step (the `HasOuterWaterNeighbor8` peeking at `read` outside the box) was not in the brief** but was necessary to make the `CoastRing_IsRelabeled_IncludingExpandedRing` test pass: without it, a plain cell sitting on the box's outer edge would never see the water that lives just outside the box, and would be silently mis-labeled as `Plain` instead of `Coast`. Strictly speaking this is a one-cell extension of the read footprint, which is in tension with hard C ("nothing outside range is touched" — *touched* meaning read *and* written). I read C as forbidding writes outside the range; the one-cell read peek is required for the spec's own "外扩 1 格" rule to take effect on the box's outermost ring. If the reviewer prefers a stricter reading (no reads outside the box at all), the alternative is to widen the box to `±2` and live with the extra cost — see Concerns §8.
- **No other YAGNI cuts.** Every other step (de-island, fill-lake, morphology, coast relabel, Changed-as-deletion encoding, stats counts) is implemented as the brief specifies.

## 8. Concerns / questions

1. **Untracked files left behind by T1–T3.** During T4, the working tree contained 10 untracked items that had been created by T1 (`.md` plan + 2 specs) and T2/T3 (7 Godot `.uid` sidecars). The first T4 commit (`4ab54ee`) inadvertently swept them all in; I detected this from the diff stat, soft-reset, and re-committed with `git add` scoped to the two T4 files only (`bf8f441`). The pre-existing untracked items remain untracked. **Recommendation for orchestrator**: a small "housekeeping" commit before T9 (or as a T10 tidy-up) to register the three `.md` files in `docs/superpowers/` and the seven `.uid` files in their proper places. They are NOT part of T4's deliverable, and I have not touched them.

2. **Box-edge coast correctness vs. hard C.** I implemented the coast relabel by reading one extra cell beyond the box for the box-edge case. Strictly, this is one `read` call outside `[rangeMin..rangeMax] + 1`, which is necessary to make "外扩 1 格" actually deliver coast labels on the box's outermost ring. The brief is silent on whether a one-cell read peek is permitted; the spec's "扫描盒外扩 1 格" strongly implies yes. If the reviewer wants a stricter "no read outside the box at all" semantics, the right fix is to widen the box to `±2` (or accept a 1-cell-wide under-labeling on the very outermost ring). My read: brief ambiguity → most-conservative reading = "writes never escape the box; reads of the immediate 1-cell halo are required for spec compliance." Documented here so the next reviewer can override.

3. **Connectivity choice (4-conn for islands/lakes, 8-conn for coast).** The brief says "去碎岛" and "填碎海" without specifying the connectivity. The spec is also silent. I chose 4-conn because: (a) it matches the visual intuition of a "1-cell-wide" inlet being part of the same lake as the body it connects to; (b) it is what `Equals` / `Equals-on-Type` would naively mean to a human ("a tile is a tile, neighbors are up/down/left/right"); (c) the brief's 8-conn rule for coast is explicit, so by exclusion the others are 4-conn. If the reviewer wants 8-conn for islands/lakes too (which would preserve diagonal 1-cell-wide channels as distinct), the change is one constant in each `FloodFill*` helper.

4. **`ErosionZero_DoesNotRunMorphology` semantics.** I interpreted "morphology does not run" as "no extra deletions from erosion" (the test pins `Stats.RemovedCells == 3`). The brief does not specify what morphology *would* do precisely; the spec mentions "削平单格凸起与 1 格宽毛刺" without thresholds. My implementation uses a 3-of-4-neighbors threshold for both erode and dilate, alternating, for `Erosion` rounds. This is a defensible default but is the most likely place a reviewer will want a tweak — and I have left it in the implementation rather than gating it behind a T4-test-only stub. If the orchestrator wants a T4a to refine the morphology rule, the helper pair `ErodeLand` / `DilateLand` is the surgical site.

5. **Test #2 (`BoundaryTouching_Component_Is_Preserved`) wording.** Hard A says "永不删除/填充". My test asserts `Stats.RemovedCells == 0` AND that any `Changed` entry for the boundary-touching cell is NOT `defaultType` (i.e., not a deletion — it may be a coast relabel). I believe this is the correct reading: hard A forbids deletion/fill, not relabel. If the reviewer interprets hard A more strictly (no `Changed` entries for these cells at all), the test would need a different boundary-touching layout where the cell has no water 8-neighbor and therefore is not relabeled either.

6. **No `using Godot;` in `CoastlineRefiner.cs`.** Confirmed: only `using WorldView.Domain;` and `namespace WorldView.Domain.Generation;`. The file is a pure C# / .NET 8 library and the existing xunit runner picks it up without any Godot involvement.
