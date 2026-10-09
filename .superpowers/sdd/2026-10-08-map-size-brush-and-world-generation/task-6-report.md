# Task 6 Report — TerrainDefaults.Water + 生成参数校验

## Status
DONE

## Commit
e20778e feat(domain): 新增 TerrainDefaults.Water + W_GENERATION_PARAM 校验（plan L259-275 Task 6）

## TDD evidence
- RED: 仅加测试文件时 `WorldView.Tests` 编译失败，1 个错误 `CS0117 “TerrainDefaults”未包含“Water”的定义 (MapValidatorGenerationTests.cs:21)` — 与预期 100% 吻合（`GenerationSpec`/`MapDoc.Generation` 已在 T3 落地，不构成编译错误）。
- GREEN: `dotnet exec ... -class WorldView.Tests.Validation.MapValidatorGenerationTests` → `Total: 4, Errors: 0, Failed: 0, Skipped: 0, Time: 0.066s`。补充定向回归 `-class MapValidatorTest` → 6/6、 `-class MapValidatorExtraTests` → 9/9，确认新规则未影响既有测试。
- REGRESSION: 全量 `dotnet exec ...`（无 `-class`）→ `Total: 147, Failed: 7`。基线 143/7 → 147/7，4 个新测试全过，7 个失败名与基线一致（`UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`、`UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`、`BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`、`BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`、`YamlSchemasTest.Deserialize_GridCoord_As_Array`、`ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`、`ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`），均与本任务无关。

## What was built
- `scripts/Domain/Terrain.cs`: 在 `Plain` 之后新增 `public static TerrainCell Water => new(TerrainType.Water, 999, false);`，附 XML 文档说明不可建造是硬要求、`move_cost` 为 `[待标定]` 占位值。
- `scripts/Domain/Validation/ValidationError.cs`: 在 `LayerTypeMismatch` 之后新增 `GenerationParamOutOfRange(path, field, value, range)` 工厂，封装 `W_GENERATION_PARAM` 警告（仅供未来 UI 调用；本任务 `MapValidator` 使用内联 `new(...)` 直接复用 `DescribeViolation` 的中文消息）。
- `scripts/Domain/Validation/MapValidator.cs`: 规则 8 —— `map.Generation is not null` 时调用 `GenerationLimits.DescribeViolation(map.Generation)`，越界则 `warnings.Add(new ValidationError(Severity.Warning, "W_GENERATION_PARAM", path, violation))`，不阻塞 save。同时把规则 8 加入类 XML 文档的 `<item>` 列表。
- `tests/WorldView.Tests/Validation/MapValidatorGenerationTests.cs`: 4 个 `[Fact]` 覆盖 `Water` 常量、`land_ratio` 越界产生警告、合法参数不产生警告、`Generation` 为 `null` 不产生警告。

## Concerns (if any)
无。

## Spec coverage
- [x] Water_Constant_Is_NotBuildable
- [x] GenerationParam_OutOfRange_Produces_Warning
- [x] GenerationParam_InRange_Produces_No_Warning
- [x] MapWithout_Generation_Produces_No_Warning
- [x] MapValidatorTest not regressed (6/6)
- [x] ProjectValidatorTests not regressed (仅 `CharacterIndex_Missing_File_Is_Warning_Only` 一个失败，基线即失败，与本任务无关)
- [x] MapValidatorExtraTests not regressed (9/9)