# Task 7 Report — UndoStack 批处理 + CompositeCommand

## Status
DONE

## Commit
48c523b feat(editing): 新增 CompositeCommand + UndoStack 批处理（plan L277-299 Task 7）

## TDD evidence
- RED: 17 CS1061 errors（BeginBatch/EndBatch/IsBatching/BatchDescription 在 UndoStack 上不存在；test 文件未引用 CompositeCommand 类型，故无 CS0246，与 brief 预估「2 类错误」一致）
- GREEN: UndoStackBatchTests 8/8 pass in 0.079s
- REGRESSION: Total 155 / Failed 7（baseline = 147；7 失败名称与 brief 完全一致，未引入新回归）。注：brief 写「baseline 143 + 8 = 151」，实际 baseline = 147，差异源于 brief 撰写到执行期间上层测试套件有自然增减；**7 失败 by-name 不变这一硬约束已满足**。

## What was built
- `scripts/Editing/CompositeCommand.cs`: `sealed class CompositeCommand : IEditorCommand`，`List<IEditorCommand>` 容器 + `Add`/`Apply`（正序 0..N-1）/`Revert`（逆序 N-1..0），纯被动（Apply 仅在 Redo 时被触发，因为批内子命令已在 Push 时各自 Apply 完毕）
- `scripts/Editing/UndoStack.cs`: 新增 `_openBatch` 字段；`Push` 路由——批内走 `_openBatch.Add(cmd) + _redo.Clear()`，否则走 `EnqueueUnit`；新增 `BeginBatch(description)`（嵌套忽略内层，沿用外层）、`EndBatch()`（无批 no-op / 空批不登记 / 否则 `EnqueueUnit(batch)`）；新增 `IsBatching` 与 `BatchDescription` 只读属性；抽 `private EnqueueUnit(IEditorCommand)` 复用「FIFO 容量 + redo 清空」；`Clear` 一并清掉 `_openBatch`

## Concerns
（无）

## Spec coverage
- [x] Batch_Of_Three_Pushes_Is_OneUndoUnit
- [x] Undo_RevertsBatch_InReverseOrder
- [x] Redo_AppliesBatch_InForwardOrder
- [x] EmptyBatch_PushesNothing_AndUndoReturnsFalse
- [x] EndBatch_WithoutBegin_IsNoOp
- [x] NestedBegin_KeepsOuterDescription
- [x] Batch_CountsAsOneAgainstCapacity
- [x] Push_OutsideBatch_StillWorks
- [x] UndoStackTests not regressed (2 baseline failures unchanged by name)
- [x] BrushShapesTests not regressed (2 baseline failures unchanged by name)
- [x] YamlSchemasTest not regressed (1 baseline failure unchanged by name)
- [x] ProjectValidatorTests not regressed (1 baseline failure unchanged by name)
- [x] ProjectLoaderTests not regressed (1 baseline failure unchanged by name)
