# Task 9 Report — `RefineCoastlineCommand`

**Status**: DONE
**Commit**: `9789af1 feat(editing): Task 9 - RefineCoastlineCommand (IEditorCommand wrapper for T4)`
**BASE**: `4010c62 feat(storage): Task 8 - map_index 追加 + R12 YamlDotNet null-skip`
**Branch**: `deepseek-build`

## 1. Verbatim signatures

```csharp
public sealed class RefineCoastlineCommand : IEditorCommand
{
    public RefineCoastlineCommand(
        EditorContext context,
        string mapId,
        GridCoord rangeMin,
        GridCoord rangeMax,
        int radiusCells,
        GenerationSpec? spec = null);

    public string Description { get; }   // "细化海岸线（去碎岛 {Removed} · 填碎海 {Filled}）" / "细化海岸线"

    public static RefineOptions OptionsFor(int radiusCells, GenerationSpec? spec);
    public static RefineOutcome Compute(MapDoc map, GridCoord rangeMin, GridCoord rangeMax, RefineOptions options);
    public static MapDoc ApplyTo(MapDoc map, RefineOutcome outcome);
    public static MapDoc RevertTo(MapDoc map, RefineOutcome outcome, IReadOnlyList<TerrainCellEntry>? beforeCells = null);

    public void Apply();
    public void Revert();
}
```

## 2. TDD evidence

| # | [Fact name] | RED | GREEN | regression |
|---|---|---|---|---|
| 1 | `OptionsFor_Radius1_And_2_RelabelOnly` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 2 | `OptionsFor_Radius8_Gentle` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 3 | `OptionsFor_Radius32_Full_UsesSpecErosion` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 4 | `Compute_RemovesThreeCellIsland_AtDefaultThreshold` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 5 | `ApplyTo_WritesCoastRing` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 6 | `ApplyTo_DeletesEntriesEqualToDefault` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 7 | `RevertTo_RestoresExactBeforeState` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 8 | `ApplyTo_KeepsCellsSortedByYThenX` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |
| 9 | `Description_CountsMatchStats` | stub → NotImplementedException | ✅ pass | Total 169 / Failed 7 |

**Full suite**: `Total: 169, Errors: 0, Failed: 7, Skipped: 0` — exactly matches baseline (160/7) + 9 new tests, 0 new failures.

## 3. Files

| File | Status | Lines |
|---|---|---|
| `scripts/Editing/RefineCoastlineCommand.cs` | new | 216 |
| `tests/WorldView.Tests/Editing/RefineCoastlineCommandTests.cs` | new | 270 |

## 4. Spec coverage checklist (9 named [Fact]s)

- [x] `OptionsFor_Radius1_And_2_RelabelOnly`
- [x] `OptionsFor_Radius8_Gentle`
- [x] `OptionsFor_Radius32_Full_UsesSpecErosion`
- [x] `Compute_RemovesThreeCellIsland_AtDefaultThreshold`
- [x] `ApplyTo_WritesCoastRing`
- [x] `ApplyTo_DeletesEntriesEqualToDefault`
- [x] `RevertTo_RestoresExactBeforeState`
- [x] `ApplyTo_KeepsCellsSortedByYThenX`
- [x] `Description_CountsMatchStats`

## 5. Self-chosen decisions

- **`RefineOutcome` field names** (grep'd `scripts/Domain/Generation/CoastlineRefiner.cs:438`):
  - `IReadOnlyDictionary<GridCoord, TerrainType> Changed`
  - `RefineStats Stats`
  - `RefineStats = record(int RemovedCells, int FilledCells, int RelabeledCells)`
  - `RefineOptions = record(int MinIslandCells = 8, int MinLakeCells = 6, int Erosion = 0, bool Morphology = false)`

- **`Refine` signature** (real, differs from brief §3.1):
  - `CoastlineRefiner.Refine(Func<int,int,TerrainType> read, int width, int height, GridCoord rangeMin, GridCoord rangeMax, TerrainType defaultType, RefineOptions options)`
  - The brief's snippet at lines 78-85 (passing `map.TerrainData.Cells` directly) is **wrong** — `Cells` is `IReadOnlyList<TerrainCellEntry>`, not `Func<int,int,TerrainType>`. Built the closure inside `Compute`.

- **`FindMap` signature**: `MapDoc? FindMap(string mapId)` (at `scripts/Editor/EditorContext.cs:227`). Confirmed.

- **`Cells` 升序 OrderBy**: called exactly **1** time per `ApplyTo` / `RevertTo` (in the `byCoord.Values.OrderBy(e => e.At.Y).ThenBy(e => e.At.X).ToList()` chain). Matches the T8 hint at `EditorContext.cs:393-396`.

- **`Description` template**: `$"细化海岸线（去碎岛 {_outcome.Stats.RemovedCells} · 填碎海 {_outcome.Stats.FilledCells}）"` — uses **U+00B7** (·) middle dot, NOT U+30FB (・) fullwidth. The test asserts both presence of `\u00B7` and absence of `\u30FB`.

- **R18 follow-up**: `scripts/Storage/Yaml/WorldIndex.cs:24` **already has** the `string.IsNullOrEmpty(refPath)` guard:
  ```csharp
  if (string.IsNullOrEmpty(refPath)) throw new ArgumentException("refPath 不可为空", nameof(refPath));
  ```
  **No change needed.** Skipped.

- **`RevertTo` signature extension**: Added optional `IReadOnlyList<TerrainCellEntry>? beforeCells = null` parameter (third arg). Instance path (`Apply` / `Revert`) captures `_beforeCells` from the map snapshot at `Apply()` time, then passes it on `Revert`. The brief's terse "restore exact before state" wouldn't otherwise be possible since `RefineOutcome.Changed` is forward-only.

## 6. Concerns

None.

## 7. Notes for T11 (parent task)

- `RefineCoastlineCommand.OptionsFor` uses **cell radius**, not world radius. T11 integration must call `MapSizing.CellsForLength(brushWorldRadius, map.Grid.CellDistance)` before passing to the command (per brief §11.4).
- The instance path (`Apply` / `Revert`) does **not** push to `UndoStack` — caller responsibility. T11 should wrap brush + refine in `UndoStack.BeginBatch("笔触细化") / Push(brushCmd) / Push(refineCmd) / EndBatch()`.
- Default fallback for `GenerationSpec? spec = null` uses `Erosion ?? 1` for the >8 radius tier.
- Description template uses U+00B7 (`·`). Don't change it.