# Task 2 Report — B2 测试基线采集

Plan: `docs/superpowers/plans/2026-10-10-p0-c2-acceptance-baseline.md`
Brief: 见 ledger · 档位：轻档 · 采集时间：2026-10-10 05:36–05:40 (+0800)

## 命令

```bash
dotnet build WorldView.sln -c Debug            # 产出 exe
DOTNET_CLI_UI_LANGUAGE=en-US dotnet exec tests/WorldView.Tests/bin/Debug/net8.0/WorldView.Tests.dll
```

> **首选命令（exe 直跑）在本机不可用**：`Failed to load the dll from [...hostfxr.dll 10.0.12], HRESULT: 0x80070005`，沙箱内外均复现。改用 `dotnet exec <dll>`。加 `DOTNET_CLI_UI_LANGUAGE=en-US` 防中文乱码。

## 结果（连跑 4 次）

| 次数 | total | failed | succeeded | skipped | 备注 |
| --- | --- | --- | --- | --- | --- |
| run 0 | 351 | 9 | 342 | 0 | 首次（中文输出，1m02s） |
| run 1 | 351 | 8 | 343 | 0 | |
| run 2 | 351 | 8 | 343 | 0 | 与 run 1 失败集一致 |
| run 3 | 351 | 11 | 340 | 0 | 与 run 1 差异 4 条 |

**总数稳定 351**（§6.4 记录为 344，+7）；**失败数在 8~11 之间浮动 → 存在 flaky，无单一“基线失败数”**。

## 失败项划分

### A. 稳定集（7 条，每次必失败）——与 §6.4 逐条对应

| §6.4 行 | 测试 |
| --- | --- |
| 1 | `Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue` |
| 2 | `Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric` |
| 3 | `Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells` |
| 4 | `Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells` |
| 5 | `Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array` |
| 6 | `Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute` |
| 7 | `Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only` |

✅ **7 条与 §6.4 表逐行对应，无遗漏、无多余。**

### B. Flaky 集（6 条，全部在 `ProjectManagement`）

| 测试 | 观测到的失败签名 |
| --- | --- |
| `BackupServiceTests.PreOpenRotation_Keeps_Most_Recent_10` | `Expected: 10 / Actual: 11`（磁盘目录数），耗时 19s |
| `BackupServiceTests.List_Returns_Manifest_In_CreatedAt_Order` | `Expected: 3 / Actual: 2`（清单条数），耗时 2.8s |
| `BackupServiceTests.Delete_Removes_Content_And_Manifest_Entry` | 计数/存在性断言 |
| `ProjectWriteCoordinatorTests.WriteFileAtomic_Overwrites_Existing_File` | 仅 run1 出现 |
| `RecentProjectServiceTests.RecordOpened_Dedupes_By_Path` | `Assert.Equal() Strings differ` |
| `RecentProjectServiceTests.RecordOpened_Caps_At_10_Oldest_Evicted` | `Assert.DoesNotContain() Filter matched`，耗时 5.8s |

每次运行从中随机命中 1~4 条 → 这是失败数在 8~11 间浮动的全部来源。

## 与 plan 的偏差

1. plan 要求「与 §6.4 已实测量 344/337/7 对账」——实测总数已变为 **351**，且失败数为**区间**而非定值。已在报告中显式记录，最终口径由 T4 基线文档确立。
2. plan 未预见 **flaky**。已按 T3 职责转为「§6.4 未覆盖的新失败 → 补定性」。

## 结论

- **B2 基线 = 总数 351 / 成功 343 / 稳定失败 7 / flaky 1~4（不定）**。
- 稳定 7 条已由 §6.4 定性，结论有效。
- flaky 6 条为新增项，见 T3 报告与 §6.4 新增节。
