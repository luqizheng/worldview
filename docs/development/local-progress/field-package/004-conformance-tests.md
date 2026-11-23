# 004 — Field Package Conformance / Fuzz / Resource Limits

**日期**：2026-11-23
**分支**：`feat/field-package-reader-writer`（PR #17 head，stacked on PR #11）
**状态**：Windows 实施完成，587/587 通过；macOS 待 GitHub Actions

## 1. 任务目标

继续完善 Field Package 规范测试：为每个规范约束建立对应测试（Header/Manifest/Directory 非法内容、tile 坐标顺序、offset/长度溢出、payload 越界重叠、padding、tile hash、mask、codec 配置、超大输入、截断、异常流、资源上限），补充有界可复现的 fuzz 测试；不引入外部依赖、不为测试放宽生产校验。

## 2. 实际完成内容

新增 `tests/WorldView.Application.Tests/Packaging/FieldPackageConformanceTests.cs`（38 个用例），测试名携带 P2-09-36 稳定向量 ID。既有四个测试文件已覆盖的向量**不重复**，映射见 §4。

### 生产代码修复（测试驱动的真实缺陷）

`FieldPackageReader.ReadTile` 在读 stored payload 遇到截断/I/O 失败时此前抛**裸** `EndOfStreamException`/`IOException`，违反 ME-05-10 §10 错误分型（Open 路径有映射、ReadTile 路径缺失）。修复：与 Open 相同的映射——`EndOfStreamException→WVPK_TRUNCATED`、`IOException/UnauthorizedAccessException→WVPK_IO`。未放宽任何校验。

### Fuzz（确定性、有界、可复现）

- `Fuzz_MutatedPackagesAndManifestsOnlyProduceTypedErrors`：固定种子 `20261123`，对三个 golden 包做 240 轮 1–4 字节随机翻转；打开成功则全 tile 读取。断言：只有 `FieldPackageValidationException` 且 Code ∈ WVPK 词表可逃逸；任何其他异常 = 缺陷。总运行时间 0.43s（全部 38 用例）。
- `Fuzz_TruncatedInputsOnlyProduceTypedErrors`：每 7 字节一个前缀的全量截断扫描 + 60 轮随机截断点；断言截断输入永不成功打开且只产生类型化错误。

## 3. 修改文件清单

| 文件 | 变更 |
|---|---|
| `WorldView/tests/WorldView.Application.Tests/Packaging/FieldPackageConformanceTests.cs` | 新增：38 个 conformance/fuzz/limits 用例 |
| `WorldView/src/WorldView.Application/Packaging/FieldPackageReader.cs` | 修复：ReadTile 的 I/O 异常映射为 WVPK_TRUNCATED / WVPK_IO |
| `docs/development/local-progress/field-package/004-conformance-tests.md` | 本报告 |

## 4. P2-09-36 向量 → 测试映射表

| 向量 | 测试 | 位置 |
|---|---|---|
| RDR-001 | OpenAndReadTiles_ValidatesCanonicalGoldenPackages | ReaderTests（既有） |
| RDR-002 | Open_RejectsTruncatedHeader | ReaderTests（既有） |
| RDR-003 | Open_RejectsInvalidMagicBeforeParsingManifest + RDR_003_RejectsInvalidHeaderFields（version/header_length/flags/reserved） | ReaderTests（既有）+ Conformance（新增） |
| RDR-004 | Open_RejectsDeclaredPhysicalLengthMismatch | ReaderTests（既有） |
| RDR-005 | RDR_005_RejectsManifestOffsetAndSizePolicy | Conformance（新增） |
| RDR-006 | Open_RejectsNonzeroManifestAlignmentPadding | ReaderTests（既有） |
| RDR-007 | RDR_007_RejectsDirectoryArithmeticOverflow（length/count 溢出） | Conformance（新增） |
| RDR-008 | Open_RejectsNonzeroDirectoryEntryFlags + RDR_008_RejectsDirectoryVersionAndEntrySize | ReaderTests（既有）+ Conformance（新增） |
| RDR-009 | Open_RejectsDirectoryEntryCountMismatch（含公式 ceil 校验） | ReaderTests（既有） |
| RDR-010 | RDR_010_RejectsTileCountAboveConfiguredPolicy | Conformance（新增） |
| RDR-011 | RDR_011_RejectsDuplicateMissingAndOutOfGridCoordinates（缺失经由 count≠公式拒绝） | Conformance（新增） |
| RDR-012 | RDR_012_RejectsEdgeTileDimensionMismatch | Conformance（新增） |
| RDR-013 | RDR_013_RejectsDecodedLengthInconsistentWithManifestPlanes | Conformance（新增） |
| RDR-014 | RDR_014_RejectsInvalidCodecRegistry（rename/parameters） | Conformance（新增） |
| RDR-015 | Open_RejectsUnsupportedTileCodec + RDR_015_RejectsTileCodecOutsideRegistry | ReaderTests（既有）+ Conformance（新增） |
| RDR-016 | Open_RejectsTilePayloadRangeOutsidePackage + RDR_016_RejectsMisalignedAndOverlappingPayloadRanges | ReaderTests（既有）+ Conformance（新增） |
| RDR-017 | RDR_017_RejectsNonzeroPayloadGapAndTrailingBytes | Conformance（新增） |
| TIL-001 | TIL_001_ReadsOnlyTheRequestedTilePayloadRange（TrackingReadStream 断言读范围） | Conformance（新增） |
| TIL-002 | golden tile 读取（ReadTile 内先验 hash 后暴露） | ReaderTests（既有，成功路径） |
| TIL-003 | TIL_003_RejectsTruncatedStoredTile（驱动 ReadTile 修复） | Conformance（新增） |
| TIL-004 | TIL_004_RejectsCodecVersionPinnedOtherThanNoneV1 | Conformance（新增） |
| TIL-005 | TIL_005_RejectsStoredLengthDifferentFromDecodedLengthForCodecZero | Conformance（新增） |
| TIL-006 | codec 0 stored==decoded + 范围检查 ⇒ 尾随编码数据归入 RDR-016/017 | 映射说明 |
| TIL-007 | ReadTile_RejectsInvalidValidityMaskAfterHashVerification | ReaderTests（既有） |
| TIL-008 | ReadTile_AcceptsDistinctExplicitCoverageMask / RejectsInvalidExplicitCoverageMaskValue / Open_RejectsCoverageMaskAliasingValidityMask | ReaderTests（既有） |
| TIL-009 | ReadTile_RejectsPayloadHashMismatch（异常抛出，无部分 tile 返回） | ReaderTests（既有） |
| TIL-010 | golden 测试中 ReadTile(1,0) 抛 Directory | ReaderTests（既有） |
| TIL-011 | TIL_011_RejectsDecodedTileAboveConfiguredBudget | Conformance（新增） |
| WRT-001 | WRT_001_RepeatedWritesAreByteIdentical | Conformance（新增） |
| WRT-002 | Write_ReproducesCanonicalGoldenPackageByteForByte ×3 | WriterTests（既有） |
| WRT-003 | WRT_003_WriterSpoolFilesNeverSurviveACompletedWrite + Limits_WriterMaximumPackageBytesIsEnforcedDuringSpooling；全尺寸大字段基准归 S10 平台证据 | Conformance（新增）+ 延后项 |
| WRT-004 | Write 内建 reopen 验证 + WRT_004_WriterOutputReopensThroughProductionReader | Writer 行为 + Conformance（新增） |
| WRT-005 | Write_RejectsMissingTileInsteadOfPublishingSparseGrid + WRT_005_RejectsDuplicateOutOfOrderOutOfGridAndExtraTiles | WriterTests（既有）+ Conformance（新增） |
| WRT-006 | WRT_006_RejectsCodecZeroWithParameters（非 none codec 不可注册，见 RDR-014） | Conformance（新增） |
| WRT-007 | WriteFileAtomically 失败清理（WRT_007_WRT_008）；Write API 无 CancellationToken——见 §6 偏差 | Conformance（新增，部分） |
| WRT-008 | WRT_008_DiskFullDestinationSurfacesTypedIoError + 原子写失败清理 | Conformance（新增） |
| INT-001 | Verify_MatchesExpectedHash…/RejectsMismatch…/RejectsMalformedExpectedDigest | IntegrityVerifierTests（既有） |
| INT-002 | INT_002_PackageHashMismatchIsDistinctFromTileHashMismatch | Conformance（新增） |
| INT-003 | **未实施**——V1 Reader 明确单操作非线程安全（类文档），见 §6 | 延后项 |
| INT-004 | INT_004_DisposedReaderRejectsFurtherUse（V1 无池化 buffer，byte[] 按读返回） | Conformance（新增） |
| INT-005 | 延后至 GitHub Actions macOS CI 证据 | 延后项 |

P2-09-33（binary envelope 28 例）与 RDR-002..008 同族，由上表对应测试覆盖。任务清单 16 项（Header 非法字段/Manifest 非法内容/Directory 非法结构/Tile 坐标顺序/Offset 长度溢出/Payload 越界重叠/非法 Padding/Tile Hash/非法 Mask/非法 Codec/超大 Manifest/超大 Tile 数/超大 Payload/截断输入/异常 Stream/资源上限）全部有对应测试或既有测试。

## 5. Windows 编译与测试结果

- `dotnet build -c Debug` / `-c Release`：成功，0 错误。
- 新增 38 用例全通过；全解决方案 Debug：**587/587**；Release：**587/587**。

### 执行的测试命令

```text
dotnet build -c Debug --nologo
dotnet test WorldView.sln -c Debug --nologo     # 587/587
dotnet build -c Release --nologo -v q
dotnet test WorldView.sln -c Release --nologo   # 587/587
# 单类过滤：
dotnet tests/WorldView.Application.Tests/bin/Debug/net11.0/WorldView.Application.Tests.dll \
  -class "WorldView.Application.Tests.Packaging.FieldPackageConformanceTests"
```

## 6. 未解决问题 / 与原设计不一致的地方

1. **INT-003（并发位置读）未实施**：`FieldPackageReader` 类文档明确 V1 为单操作、非线程安全（流位置独占）。并发读支持属 ME-05-11 S9 范围的产品能力扩展，非测试可弥补；在未获产品裁决前未擅自改并发语义。
2. **WRT-007 部分覆盖**：失败清理已测（临时文件/spool 全部移除、最终产物不出现）；但 `FieldPackageWriter.Write` API 本身无 `CancellationToken` 参数，"取消"只能经由失败注入模拟。向量完整语义需 API 增补，属实现面扩展。
3. **WRT-003 全尺寸基准**：有界 spool 行为已测（spool 文件不残留 + 写入中预算强制）；代表数据集的吞吐/内存基准归 ME-05-11 S10 平台证据，未虚构数字。
4. **INT-005 跨平台字节一致**：需 macOS CI 实测（golden 复现测试已为逐字节断言，macOS 跑同一套即得证据）。
5. 修复 ReadTile 错误映射属于**补齐**类型化错误契约（ME-05-10 §10 本就要求）；未新增/删除任何校验规则，未放宽生产校验。

## 7. 需要 GitHub Actions 验证的内容

- macOS Debug/Release 全部 587 测试（含 38 个 conformance/fuzz 用例；fuzz 固定种子可复现）。
- INT-005：三 golden 包在 macOS 上逐字节复现 + 相同错误码（WRT-002 与本套件即为断言载体）。

## 8. Git commit SHA

见本报告提交后的 `feat/field-package-reader-writer` 分支顶端（提交信息 `test(packaging): complete field package conformance fuzz and limits`）。
