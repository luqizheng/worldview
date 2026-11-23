# 005 — Import/Export Authority Boundary

**日期**：2026-11-23
**分支**：`feat/field-package-reader-writer`（PR #17 head，stacked on PR #11）
**状态**：Windows 实施完成，Debug/Release 各 606/606 通过；macOS 待 GitHub Actions

## 1. 任务目标

修正代码审查发现的 Task 003 架构边界问题：Importer 此前把整个 WVFLDPK 文件作为 Field Payload 存储，并把 package SHA-256 同时用作 payload ContentHash 与 Revision ContentFingerprint，混淆了交换文件完整性与内部内容指纹。本任务按 ME-05-12 §4/§6/§7 重建权威边界：Field Package 只是导入导出交换格式；SQLite 是 Dataset/Revision 元数据权威；Field Payload BLOB/ManagedFile 是运行时数据权威；package digest 仅用于交换验证。同时补齐完整 Export Adapter、Production Profile Gate 与 WRT-007 取消缺口。

## 2. 实际完成内容

### 2.1 内部 Field Payload V1 格式

- 新增 `FieldInternalPayloadFormat`：`application/vnd.worldview.field-payload` v1（沿用既有标识，本次明确其数据布局）。
- 布局 = 全部 decoded tile 按 canonical `(tile_y, tile_x)` 顺序串接，每个 tile 内 plane 按 manifest 声明顺序，无分隔符，无任何 WVFLDPK 帧结构（header/manifest/directory/padding 均不进入 payload）。
- 新增 `FieldInternalPayloadPlan.FromMetadata(grid, planes)`：仅从已验证的 typed manifest 投影（grid 尺寸、tile 尺寸、plane 顺序与 bytes_per_cell）推导 canonical tile 序列、每 tile 字节数与总长；全程 checked arithmetic，不可行的 grid fail-closed。导入与导出共用同一计划，布局不会漂移。
- 无历史数据，未提供旧格式兼容层。

### 2.2 Package / Payload Hash 语义（三者分离）

| 名称 | 计算对象 | 用途 |
|---|---|---|
| `PackageSha256` | 整个 WVFLDPK 交换文件字节 | 仅交换完整性验证，出现在导入/导出结果记录中，不进入 payload 元数据 |
| `PayloadContentHash` | 内部 decoded tile 串接字节的独立 SHA-256 | payload 元数据 ContentHash（`sha-256`，小写 hex） |
| `ContentFingerprint` | 同内部 payload SHA-256（符合 Domain 既有 64 位小写 hex 契约） | Revision 指纹；不再是 package digest |

导入结果 `FieldPackageImportResult` 现同时携带 `PackageSha256/PackageByteLength` 与 `PayloadSha256/PayloadByteLength`，测试断言两者必然不同。

### 2.3 Importer 修正（`FieldPackageImporter`）

流程（validate-first 保持不变，新增 gate 与独立 payload 组装）：

1. `FieldPackageReader.Open` 二进制全量校验。
2. `FieldPackageOwnerContextValidator` 归属预检（返回 typed `Metadata` 投影）。
3. Production Profile Gate（见 2.4）。
4. Package digest pass：整包流式 SHA-256（独立于 payload）。
5. 全量 tile 验证 pass：逐 tile `ReadTile`（hash+mask 按读校验），同时按 canonical 顺序增量 SHA-256 计算 payload digest；BLOB 路径同 pass 组装 payload 字节，ManagedFile 路径不保留字节（零额外内存）。
6. 发布：
   - **SqliteBlob**：payload 字节 + revision + Dataset CAS 同一 SQLite 事务，失败整体回滚。
   - **ManagedFile**：新增 `DecodedTilePayloadStream` 前向只读流（每次只驻留一个 tile，逐 tile 重新验证 hash/mask，流末尾对交付长度与独立重算 SHA-256 做缺陷守卫）喂给既有 `FieldPayloadPublisher.PutAsync`（staging→flush→hash 验证→原子 rename）；DB 发布失败仍遵循 orphan recovery。大 payload 不整包进内存。
   - 发布后缺陷守卫：已发布 payload 的 hash/length/fingerprint 必须等于验证阶段独立计算的值。

不得将原始 WVFLDPK 写入 payload 的要求由测试固化（BLOB 与 ManagedFile 双路径断言存储内容 = decoded tiles 串接 ≠ 包字节）。

### 2.4 Production Profile Gate（`FieldPackagePolicy`）

- `FieldPackagePolicy`：实例作用域记录，`AllowGoldenFixtureOnlyProfiles` 默认 **false**（fail-closed）。构造时注入 Importer/Exporter，**无任何全局/静态开关**。
- `golden_fixture_only` profile 的包（导入）与 revision（导出）默认拒绝，稳定错误码 `WVPK_POLICY_GOLDEN_FIXTURE_ONLY`（`FieldPackagePolicyErrorCodes`）；显式允许的策略实例可通过；`production` scope 无需任何策略。
- 未因三个 golden fixtures 均为 fixture profile 而放宽生产规则——测试仍需显式 `AllowGoldenFixtureOnlyProfiles = true` 才能导入 golden fixtures；另加了 production-scope manifest 的正向导入测试。
- Exporter 同样受 gate 约束（fixture-only revision 不能借导出"洗白"进生产流通），有默认拒绝测试。

### 2.5 Exporter（新增 `FieldPackageExporter`）

流程：`FindFieldRevision` 读取权威 revision → typed `Metadata` → profile gate → payload format 必须 = 内部 Payload V1（否则 `WVPK_EXPORT_PAYLOAD_FORMAT`）→ 持久化 manifest 必须字节级 canonical UTF-8（否则 `WVPK_EXPORT_MANIFEST_NOT_CANONICAL`）→ 由 grid/planes 计算 canonical tile plan 并与 `payload.ByteLength` 严格对账（否则 `WVPK_EXPORT_PAYLOAD_LENGTH`）→ 经 `FieldPayloadReader`（storage-kind-neutral，读前验证 hash+length）打开 payload → 顺序切片喂给 `FieldPackageWriter.WriteFileAtomically`（确定性 codec-0 写出、自校验、同目录原子 rename）。

约束落实：

- Exporter 不写 SQLite（revision 行与 Dataset current pointer 不变，测试固化）。
- Writer 不接触数据库；Exporter 只向它提供 manifest 字节与 canonical tile 序列。
- 不覆盖已有 package（`WriteFileAtomically` 既有语义，IOException，测试固化）。
- 不把 payload 原始字节当作 WVFLDPK 文件——总是由 Writer 从 tiles+manifest 重新生成。
- `FieldPayloadReader` 既有 API（返回已验证字节流）足以支持顺序 tile 读取（payload 是扁平串接，切片由 Application 层 `EnumeratePayloadTiles` 完成），无需扩展端口，Writer 保持纯格式化器。
- 新错误码族 `FieldPackageExportErrorCodes`：`WVPK_EXPORT_REVISION_MISSING` / `WVPK_EXPORT_PAYLOAD_MISSING` / `WVPK_EXPORT_PAYLOAD_FORMAT` / `WVPK_EXPORT_PAYLOAD_LENGTH` / `WVPK_EXPORT_MANIFEST_NOT_CANONICAL`。

### 2.6 Cancellation（WRT-007 闭合）

- `FieldPackageWriter.Write` / `WriteFileAtomically` 新增 `CancellationToken` 参数；检查点：每 tile ingestion/spool、payload copy 每 chunk、写后自校验每 tile、`WriteFileAtomically` 原子 rename 前（发布前检查）。取消后临时 spool/暂存 tmp 由既有 finally/catch 清理，正式目标文件不出现，无半成品发布。
- 取消以 `OperationCanceledException` 透传，不被 Writer 的 typed-error 包装吞掉。
- 导入路径：入口、各验证阶段之间、逐 tile 验证/组装、staging 逐 tile、发布前均有检查；ManagedFile staging 中途取消 → staging 清理、无 revision、无正式文件（测试固化）。
- Exporter：入口与发布前检查 + Writer 内部检查；pre-cancelled 导出不产生目标文件与临时文件（测试固化）。
- 无并行/线程池；取消为阶段间协作式检查（ME-05-12 §1.5）。

## 3. 修改文件清单

| 文件 | 变更 |
|---|---|
| `WorldView/src/WorldView.Application/Fields/FieldInternalPayload.cs` | 新增：内部 Payload V1 标识 + `FieldInternalPayloadPlan` checked 布局计划 |
| `WorldView/src/WorldView.Application/Fields/FieldPackagePolicy.cs` | 新增：实例作用域策略 + fail-closed gate（`WVPK_POLICY_GOLDEN_FIXTURE_ONLY`） |
| `WorldView/src/WorldView.Application/Fields/FieldPackageImporter.cs` | 重写：内部 payload 组装/独立 hash/gate/缺陷守卫 + `DecodedTilePayloadStream` |
| `WorldView/src/WorldView.Application/Fields/FieldPackageExporter.cs` | 新增：导出适配器 + `FieldPackageExportErrorCodes` |
| `WorldView/src/WorldView.Application/Packaging/FieldPackageWriter.cs` | `Write`/`WriteFileAtomically`/`CopyExactly` 增加 CancellationToken 与阶段检查 |
| `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/FieldPackageImportTests.cs` | 更新：payload 内容断言（≠包字节）、orphan 改为不同 decoded 内容、gate 正负向、3 个取消测试、staging 失败注入改为阈值式 |
| `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/FieldPackageImportExportAuthorityTests.cs` | 新增：10 个 authority/export/round-trip/cancel 测试（真实 SQLite+文件系统） |
| `WorldView/tests/WorldView.Application.Tests/Packaging/FieldPackageWriterCancellationTests.cs` | 新增：4 个 Writer 取消单元测试（确定性 cancel-on-position tile 源） |
| `WorldView/tests/WorldView.Infrastructure.Tests/WorldView.Infrastructure.Tests.csproj` | 嵌入全部 golden `*.wvfld`、edge manifest、`golden-fixtures.json` |
| `docs/development/local-progress/README.md` | 修正 002/003/004 状态，登记本报告 |

Task 001 历史报告缺失维持原状（README 注明，不补造）。

## 4. Windows 构建与测试结果

| 阶段 | 结果 |
|---|---|
| `dotnet build WorldView.sln -c Debug` | 0 错误（警告为既有 CTJ001 等，无新增类别） |
| `dotnet test WorldView.sln -c Debug` | **606/606 通过，0 失败**（Domain 223 + Application 118 + Infrastructure 265） |
| `dotnet build WorldView.sln -c Release` | 0 错误 |
| `dotnet test WorldView.sln -c Release` | **606/606 通过，0 失败** |

相对 003 基线（549）新增 19 个测试：导入类 16（+5 gate/取消），authority 10，writer cancellation 4（004 的 conformance 测试数量已计入原基线后的 Application/Infrastructure 计数）。

### 测试覆盖点对照

- Import：BLOB/ManagedFile 内容 ≠ 包字节且 = decoded 串接；payload SHA-256 与 package SHA-256 分开断言；fixture 默认拒绝 + 显式策略通过 + production scope 默认通过；腐坏包/归属不符零副作用；BLOB CAS 失败无 revision；ManagedFile CAS 失败 orphan 保留且对账回收（引用文件保留）；staging 中途 I/O 失败清理。
- Export：BLOB revision 导出；ManagedFile revision 导出；不改 current pointer/revision 行；重复导出确定性逐字节一致；拒绝覆盖已有 package；缺 revision / payload 长度与 grid 计划不符 / 未知 payload format 均为 typed 错误且无文件。
- Round Trip：minimal（BLOB 路径）与 edge（ManagedFile 路径）golden fixture Package→Import→Internal Payload→Export→Package 逐字节一致、SHA-256 一致、逐 tile 与 manifest 一致、内部 payload hash 独立且不等于 package hash。
- Cancellation：导入 pre-cancelled / 验证阶段 / staging 阶段（清理+无 revision）；Writer ingestion / 尾 tile 后（payload copy 阶段，包未完成 magic 不写入）；`WriteFileAtomically` 两阶段（无最终文件、无 tmp/spool）；export pre-cancelled（无目标文件无 tmp）。

## 5. 执行的测试命令

```text
dotnet build WorldView.sln -c Debug --nologo -v q
dotnet test WorldView.sln -c Debug --nologo     # 606/606
dotnet build WorldView.sln -c Release --nologo -v q
dotnet test WorldView.sln -c Release --nologo   # 606/606
# 单类过滤（MTP）：
dotnet tests/WorldView.Infrastructure.Tests/bin/Debug/net11.0/WorldView.Infrastructure.Tests.dll \
  -class "...FieldPackageImportTests" -class "...FieldPackageImportExportAuthorityTests"
dotnet tests/WorldView.Application.Tests/bin/Debug/net11.0/WorldView.Application.Tests.dll \
  -class "WorldView.Application.Tests.Packaging.FieldPackageWriterCancellationTests"
```

## 6. 重要实现决策与偏差

1. **ContentFingerprint 语义**：取内部 payload SHA-256（与 payload ContentHash 同值不同角色）。这是"内部 Field 内容指纹"契约的直接落法；package digest 从此只存在于导入/导出结果记录中。若调用方需要把 package digest 留痕，应自行写入其 provenance JSON（导入器不改写调用方的 canonical provenance）。
2. **ManagedFile 验证读取双 pass**：validate-first 要求腐坏包在 staging 前拒绝，因此验证 pass 读全部 tiles，staging pass 经 `DecodedTilePayloadStream` 再读一次（每次读重新验证 tile hash/mask，流末对账总长与 hash）。代价是一次重复顺序读；换来 staging 字节与验证字节的等值证明。V1 正确性优先（与 003 报告的双重整包哈希取舍同一风格）。
3. **BLOB 内存驻留**：BLOB 路径组装 payload 进内存是调用方显式存储选择的既定代价；未引入任何自动大小阈值（任务明确禁止）。BLOB 导出经 `FieldPayloadReader` 读回整块字节，同样是既有权威读端口的现状。
4. **取消触发测试为确定性注入**（阈值流/cancel-on-position 枚举），不起线程。`WriteFileAtomically` 的 rename 前检查无法用公共 API 钩子单独触发（内部 FileStream 不可注入），该行与取消清理共享同一路径（catch 删 tmp + OCE 透传），由其余取消测试覆盖同一清理语义——报告中如实说明，不虚称"已专项测试"。
5. **既有测试语义更新**：003 的两条测试按新架构修正——`Import_ManagedFilePublicationCasFailureRetainsOrphanForReconciliation` 中制造 orphan 的第二包从"manifest 不同"改为"decoded 内容不同"（manifest-only 差异在新布局下产生同一 payload、不再产生第二个文件）；staging 失败注入从"第三次回卷"改为"累计读取阈值"（staging 不再直接整包拷贝）。测试意图（orphan 保留与回收、staging 清理）不变。
6. **round-trip 逐字节一致的前提**：同确定性 profile/codec（codec 0、canonical manifest 原字节、canonical tile 序）——golden fixtures 天然满足；测试以嵌入的 canonical fixture 资产为输入。

## 7. 未解决问题 / 后续事项

1. 导入/导出仍无 UI/Application 入口（同 003 备注，属后续 UI 阶段接线）。
2. ManagedFile 验证/组装双 pass 的重复读在大包场景的实测代价未测量；如需优化可在 `IFieldPayloadFileStore` 增加外部传入 digest 的 staging 变体（未测瓶颈前不做）。
3. `profile_scope = production` 的正式生产编码（精度/类别码/codec/tile 尺寸）仍是 ME-05-12 §4 的产品决策，未由本任务引入任何默认值；生产 writer 仍处于禁用状态。
4. Field revision 的 ProvenanceJson 目前不含 package digest；若后续需要"来源包指纹"作为一等元数据，需要新的列/端口决策（涉及 CD-02 schema，另行提出）。

## 8. 需要 GitHub Actions（macOS）验证的内容

- macOS Debug/Release 构建 + 全部 606 测试（含 10 个 authority 测试：golden fixture 嵌入资源、`FieldPayloadFileStore` 原子 rename、`WriteFileAtomically` 同目录 tmp rename、取消清理路径均为平台相关行为）。
- 内容寻址 store 与临时目录在 macOS 文件系统上的 staging 清理与 `File.Move(overwrite: false)` 行为。

## 9. Git commit SHA

- 代码：commit `4f9427c`（fix(fields): store internal field payload, not raw package bytes），分支 `feat/field-package-reader-writer`。
- 本报告与 README 状态修正：随后单独 docs commit（SHA 见该提交）。

PR #17 保持 Draft，未合并任何 PR。
