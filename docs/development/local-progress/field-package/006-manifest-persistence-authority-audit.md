# 006 — Field Manifest 持久化权威审计

**日期**：2026-11-23
**分支**：`feat/field-package-reader-writer`（PR #17 head，stacked on PR #11）
**性质**：设计审计 + 两处已验证缺陷的独立修复（无 schema 变更、无迁移、无兼容层）
**状态**：Windows Debug/Release 各 608/608 通过（606 基线 + 2 个新增回归）；macOS 待 GitHub Actions

## 1. 任务目标

审计全仓库 Field Manifest / ManifestJson 的生产用途，回答五个问题：谁依赖 ManifestJson；哪些数据已有 typed Metadata 表示；typed Metadata 能否无损重建规范 Manifest；哪些信息必须作为不可变交换快照保存；是否存在 JSON 与 SQLite typed columns 双重权威。最终给出三种持久化方案的比较与推荐。本任务优先审计与设计，不删除 ManifestJson；发现明显 bug 可修复但独立记录。

## 2. 全仓库消费者清单

### 2.1 生产代码消费者（Domain / Application / Infrastructure）

| # | 文件 | Manifest 相关角色 |
|---|---|---|
| 1 | `WorldView.Domain/Fields/FieldDatasetRecords.cs` | `FieldRevisionRecord.ManifestJson`（required）— 不可变 Revision 的交换快照字段；`Metadata`（nullable）— typed 权威投影，注释明确 "ManifestJson is retained for interchange/diagnostics" |
| 2 | `WorldView.Domain/Fields/FieldDatasetPlans.cs` | `PublishFieldRevisionPlan.ManifestJson` — 发布输入（调用方携带） |
| 3 | `WorldView.Domain/Fields/FieldDatasetService.cs` | 发布权威：`FieldManifestCanonicalizer.Canonicalize`（RFC 8785）→ `ValidateManifest`（closed-world 语义验证）→ `FieldRevisionMetadata.FromValidatedManifest` 构建 typed 投影 |
| 4 | `WorldView.Domain/Fields/FieldRevisionMetadata.cs` | `FromValidatedManifest`：typed 投影唯一构建入口；`CanonicalizeJson`：codec `parameters` 的存储形式（**本次修复点**） |
| 5 | `WorldView.Domain/Fields/FieldManifestCanonicalizer.cs` | RFC 8785 JCS canonical 化（`Canonicalize`）与字节级 canonical 验证（`RequireCanonicalUtf8`，拒绝而非归一化） |
| 6 | `WorldView.Infrastructure/.../SqliteCoreDomainDatabase.Fields.cs` | 写路径：manifest canonical 固定点检查（**本次修复点**）、`MetadataEquals` 等值对账、typed 列/子行写入；读路径：**完全从 typed 列重建 `FieldRevisionMetadata`（不解析 ManifestJson）**，codec `parameters_json` canonical 固定点检查（**本次修复点**）；`ManifestJson` 原样返回 |
| 7 | `WorldView.Infrastructure/.../EfCore/CoreDomainEntities.cs` | `CoreMapFieldRevisionEntity.ManifestJson`（required string） |
| 8 | `WorldView.Infrastructure/.../EfCore/CoreDomainV1ModelConfiguration.cs` | `manifest_json` TEXT NOT NULL（无 CHECK 约束）；typed 列映射见 §3 |
| 9 | `WorldView.Application/Fields/FieldPackageImporter.cs` | 存储包 manifest **原字节**（`reader.ManifestUtf8` 的 UTF-8 解码）作为 `ManifestJson`；typed 投影来自 Owner Context 预检 |
| 10 | `WorldView.Application/Fields/FieldPackageExporter.cs` | `RequireCanonicalManifestUtf8`（非 canonical 即 `WVPK_EXPORT_MANIFEST_NOT_CANONICAL` fail-closed）→ **manifest 字节逐字写入 package**；tile 计划/长度对账全部使用 typed `Metadata` |
| 11 | `WorldView.Application/Fields/FieldPackageOwnerContextValidator.cs` | 包 manifest 字节 `RequireCanonicalUtf8` + descriptor identity 解析 + `FieldDatasetService.ValidateManifest`（与发布同权威）+ Map Space / Spatial Reference / precision / vertical 绑定检查 |
| 12 | `WorldView.Application/Packaging/FieldPackageManifestValidator.cs` | 包层 manifest 验证：1 MiB 预算、严格 UTF-8、canonical 字节验证、嵌入式 P2-09-34 Draft 2020-12 schema |
| 13 | `WorldView.Application/Packaging/FieldPackageReader.cs` | 从包中读 manifest 字节 → 全量验证 → 解析 grid/planes/mask/codec 语义 → tile 寻址；`ManifestUtf8` 暴露给上层 |
| 14 | `WorldView.Application/Packaging/FieldPackageWriter.cs` | manifest 字节 verbatim 写入 package（offset 72 起，8 字节对齐）；writer 自校验 |
| 15 | `WorldView.Application/Packaging/FieldPackageIntegrityVerifier.cs` | 整包 SHA-256（期望值由调用方提供，不依赖 manifest） |
| 16 | `WorldView.Application/Fields/FieldInternalPayload.cs` | `FieldInternalPayloadPlan.FromMetadata(grid, planes)`：**仅从 typed 投影**派生 canonical tile 布局，不读 JSON |
| 17 | `WorldView.Domain/Persistence/ICoreDomainDatabase.cs` | 端口无 manifest 概念（由 plan/record 承载） |
| 18 | `WorldView.Desktop`（UI） | **无直接消费者**（payload recovery 不触碰 manifest；导入/导出尚无 UI 入口） |

### 2.2 测试与 Golden Fixture 消费者（单独分类，不计入生产消费者）

| 类别 | 文件 | 用途 |
|---|---|---|
| Domain 测试 | `FieldManifestCanonicalizerTests` | RFC 8785 canonical/拒绝向量 |
| Domain 测试 | `TestDoubles/FakeCoreDomainDatabase.cs` | `PublishFieldRevision` verbatim 存储计划（无 canonical 检查；canonical 检查只在 SQLite 适配器） |
| Application 测试 | `FieldPackageManifestValidatorTests`、`FieldPackageReaderTests`、`FieldPackageConformanceTests`、`FieldPackageWriterCancellationTests` | 包层 canonical/schema/二进制/取消向量 |
| Infrastructure 测试 | `Cd02FieldDatasetPersistenceTests` | canonical 化、typed 等值、decimal 精度、**本次新增 2 个回归** |
| Infrastructure 测试 | `FieldPackageImportTests`、`FieldPackageImportExportAuthorityTests`、`FieldPackageOwnerContextValidationTests` | 导入/导出/round-trip；`Import_...` 断言存储的 `ManifestJson` 与 golden 包 manifest 字节**逐字节相等** |
| Golden Fixtures | `docs/architecture/core-domain/fixtures/field-package-v1/*.manifest.json` + `*.wvfld` + `golden-fixtures.json` | 三个 canonical manifest（均为纯 ASCII、codec 0 空 parameters、`golden_fixture_only` scope），嵌入为测试资源 |

## 3. Manifest 字段映射表

规范依据：`docs/architecture/core-domain/p2-09-34-field-package-canonical-manifest-schema-v0.1.json`（嵌入式 Draft 2020-12 schema）+ `FieldDatasetService.ValidateManifest`。Schema 全树 `additionalProperties: false`（closed-world），**唯一扩展点是 `codec_registry[].parameters`（任意 JSON object）**。

"可重建" = 仅凭 typed 列 + 已验证常量能否逐字节重建 canonical manifest（经 RFC 8785 归一化后与存储快照等值）。

| 字段 | Typed 表示 | 可重建 | 影响导出 | 必须保留快照 |
|---|---|---|---|---|
| `byte_order`（const `little_endian`） | 无（验证常量） | ✅ 常量 | ✅ | 否 |
| `canonicalization`（const `RFC8785-JCS-UTF8`） | 无（验证常量） | ✅ 常量 | ✅ | 否 |
| `manifest_schema.id` / `.version`（const） | 无（验证常量） | ✅ 常量 | ✅ | 否 |
| `format.major` / `.minor`（const 1.0） | 无（验证常量） | ✅ 常量 | ✅ | 否 |
| `field.field_key` | `FieldKey`（Dataset 行 + Revision 列，双写一致） | ✅ | ✅ | 否 |
| `field.schema_version` | `DescriptorSchemaVersion` | ✅ | ✅ | 否 |
| `field.semantics_id` | `SemanticsId`（registry 绑定） | ✅ | ✅ | 否 |
| `field.unit`（仅 elevation，elsewhere 禁止） | `Unit`（nullable，null ⇔ 字段不存在） | ✅ | ✅ | 否 |
| `spatial.map_space_id` | `MapSpaceId`（绑定 Map 行） | ✅ | ✅ | 否 |
| `spatial.spatial_reference_id` | `SpatialReferenceId`（绑定 Map 行） | ✅ | ✅ | 否 |
| `spatial.coordinate_precision_profile_id` | `CoordinatePrecisionProfileId`（绑定 SR 定义） | ✅ | ✅ | 否 |
| `spatial.vertical_reference_id`（仅 elevation） | `VerticalReferenceId`（nullable） | ✅ | ✅ | 否 |
| `grid.width` / `height`（safe integer ≤ 2^53−1） | `GridWidth` / `GridHeight`（INTEGER，Int64 无损） | ✅ | ✅（tile 布局） | 否 |
| `grid.tile_width` / `tile_height` | `GridTileWidth` / `GridTileHeight` | ✅ | ✅ | 否 |
| `grid.origin_x` / `origin_y` / `spacing_x` / `spacing_y`（**canonical decimal 字符串**，精度敏感） | `GridOriginX` 等四列（TEXT，**字符串原样存储，无数值转换**） | ✅（词法精度无损） | ✅ | 否 |
| `grid.axis_orientation` / `cell_registration` | `GridAxisOrientation` / `GridCellRegistration` | ✅ | ✅ | 否 |
| `coverage.mode` / `mask_plane_name` | `CoverageMode` / `CoverageMaskPlaneName`（nullable） | ✅ | ✅（reader 校验 mask plane） | 否 |
| `map_edge_semantics` | `MapEdgeSemantics` | ✅ | ✅ | 否 |
| `no_data.policy` / `mask_plane.plane_name` / `valid_value` / `invalid_value` | `NoDataPolicy` / `NoDataMaskPlaneName` / `NoDataValidValue` / `NoDataInvalidValue` | ✅ | ✅（validity plane 校验） | 否 |
| `value_encoding.profile_id` | `ValueProfileId` | ✅ | ✅（profile gate） | 否 |
| `value_encoding.profile_scope`（golden_fixture_only / production） | `ValueProfileScope` | ✅ | ✅（profile gate + export gate） | 否 |
| `value_encoding.planes[]`（**顺序敏感**：声明序 = payload 布局序） | `core_map_field_revision_plane`（`Ordinal` 保持声明序；`ux_(revision,name)` 唯一） | ✅ | ✅（payload 布局） | 否 |
| `value_encoding.category_codes`（仅 land_water；对象键经 JCS 排序） | `core_map_field_revision_category_code`（key+code 行；code 唯一索引） | ✅ | ✅ | 否 |
| `codec_registry[]`（**顺序保留**；codec 0 语义 pin） | `core_map_field_revision_codec`（`Ordinal` + `ParametersJson`） | ✅（本次修复后，parameters 存 canonical 原字节） | ✅ | 否 |
| `codec_registry[].parameters`（**唯一扩展点**，任意 JSON object） | `ParametersJson`（TEXT，修复后 = canonical manifest 中该 object 的原字节） | ✅ | ✅ | 否 |
| `tile_hash.algorithm` / `.version` | `TileHashAlgorithm` / `TileHashVersion` | ✅ | ✅（tile 哈希验证） | 否 |

**结论**：closed-world schema 下全部字段均可无损重建；`codec parameters` 修复后也字节级保留。因此方案 B/C 在当前 schema 上**技术可行**——但这不等于应当选择 B/C（见 §5）。

## 4. 已发现的问题（含独立修复记录）

### 4.1 【缺陷·已修复】写路径 canonical 固定点检查使用了错误的 canonicalizer

- **位置**：`SqliteCoreDomainDatabase.PublishFieldRevision`（Fields.cs:114-116，修复前）。
- **现象**：发布权威 `FieldDatasetService` 以 RFC 8785 JCS 归一化 manifest（JCS 对非 ASCII 与 `+`/`<`/`>`/`&`/`'` **保留原字符**）；SQLite 适配器随后用 **legacy `CanonicalJson`**（System.Text.Json 默认转义器）做固定点检查——它把同一字符转义为 `\uXXXX`。实测：`{"spatial":{"spatial_reference_id":"EPSG:4326+grid"}}` 经 legacy 输出 `"EPSG:4326\u002Bgrid"`，非 ASCII 输出 `"\u5730\u56FE..."`，固定点均不成立。
- **后果**：任何 manifest 字符串值（plane 名、codec 名/版本/参数值等自由字符串字段；经 probe 验证所有验证层都放行）只要含 `+`、HTML 敏感字符或非 ASCII，**合法 canonical 包可通过全部导入预检，却在最后一步持久化时抛 `CoreDomainDataException("must be canonical before persistence")`**。现有 golden fixtures 为纯 ASCII 且无这些字符，故既有测试全部通过——缺陷被 fixture 词汇掩盖。
- **修复**：改用 `FieldManifestCanonicalizer.Canonicalize` 固定点检查（与发布权威同一 RFC 8785 契约），错误文案同步更正。
- **验证**：新增回归 `PublishRevision_AcceptsJcsCanonicalManifestWithJcsOnlyStringSpellings`（codec 名 `zstd+tuned`、版本 `1/中文`、参数值 `值+1`）。修复前该测试失败（发布抛异常），修复后通过。

### 4.2 【缺陷·已修复】typed codec parameters 存储形式与读路径检查不一致 → 不可读 revision

- **位置**：`FieldRevisionMetadata.CanonicalizeJson`（修复前 = `JsonSerializer.Serialize(JsonElement)`，System.Text.Json 默认选项）与 `SqliteCoreDomainDatabase.MapFieldRevision` 的 `parameters_json` 固定点检查（修复前 = legacy `CanonicalJson`）。
- **现象**（probe 实证）：
  - STJ `Serialize(JsonElement)` **保留数字 raw text 与文档键序**：`1e-7` → `1e-7`（同时把非 ASCII 转义为 `\uXXXX`）。
  - legacy `CanonicalJson` 对同一 JSON：`1e-7` → `1E-07`、`1e21` → `1E+21`；超 Int64 整数 `123456789012345678901234567890` → `1.2345678901234568E+29`（**精度破坏性改写**）。
- **后果**：codec `parameters` 含指数形式 double（JCS canonical 拼写本就是 `1e-7`——包级 canonical 字节验证可正常通过）或超 Int64 整数的 revision **可以发布成功**（写路径两边都由同一 manifest 构建、恰好相等），但**首次读取即抛 `CoreDomainDataException("non-canonical codec parameters")`**——revision 从此不可读、不可导出。
- **修复**（两处，同一契约）：
  1. `CanonicalizeJson` 改为 `FieldManifestCanonicalizer.Canonicalize(value.GetRawText())`——存储的 `ParametersJson` 从此 **byte-identical 等于 canonical manifest 中该 parameters object 的原字节**；
  2. `MapFieldRevision` 的 `parameters_json` 固定点检查改用 `FieldManifestCanonicalizer`。
- **存量兼容性**：修复前的写路径 manifest 固定点检查（legacy）保证已存在的行都是"既 JCS canonical 又 legacy 固定点"的 manifest（纯 ASCII、无指数数字拼写），其 `ParametersJson`（STJ raw text 形式）与新检查（JCS）天然一致——**不存在会因本修复变为不可读的存量行**，无需迁移。
- **验证**：新增回归 `PublishRevision_KeepsExponentFormCodecParametersByteIdenticalAndReadable`（`{"quantum":1e-7}`）。修复前 `FindFieldRevision` 抛异常、测试失败；修复后通过，且断言 `ParametersJson == {"quantum":1e-7}` 与 manifest 子串逐字节一致。

**验证方法说明**：两处修复均先临时回退、以真实 SQLite 适配器重跑（63 测试中恰好这 2 个失败、61 通过），再恢复修复全绿——回归测试确实钉住了旧行为，而非恒真。

### 4.3 【未修复·设计观察】ManifestJson 与 typed 列之间无持久化密码学绑定

`ContentFingerprint` = 内部 payload SHA-256（Task 005 §2.2 决策，正确地不是 package digest，也不含 manifest）。manifest ↔ payload 的绑定是**结构性的**（manifest 派生的 tile 计划总长 == `payload.ByteLength`，导入/导出双向对账）而非密码学的。若 `manifest_json` 列被绕过应用层篡改：读路径不重验 canonical、也不与 typed 列对账（设计如此：读路径零 JSON 解析）；导出路径有间接防线（canonical 字节验证 → writer 从 manifest 重新推导布局并自校验 → tile 计划对账），大概率 fail-closed，但不是显式的完整性检查。V1 单机、无并发写入威胁模型下可接受；若需强绑定，需新增 `manifest_canonical_sha256` 列（涉及 CD-02 schema，**需产品/架构决策**，见 §9）。

### 4.4 【未修复·设计观察】读路径按当前 registry 校验 descriptor pin

`MapFieldRevision` 读路径要求 `SemanticsId` 与**当前** `FieldDescriptorRegistry`（静态代码）一致，否则 fail-closed。未来 descriptor 演进（新增 schema_version 2；极端情形移除 v1）会使旧 revision 不可读。这与 manifest 持久化方式无关（typed 列同样受影响），属 registry 演进策略问题，**需产品决策**（如 "registry 只增不删" 规则化）。

### 4.5 【确认·非问题】不存在 JSON / typed columns 双重权威

职责现状：**typed 列是全部领域读的权威**（`MapFieldRevision` 从列重建 metadata，零 JSON 解析；owner-context 校验、payload 布局计划、profile gate 全部走 typed）；**ManifestJson 只服务两件事**——导入时保存包 manifest 原字节、导出时 verbatim 写回包。写路径另有三重对账钉住两者一致：服务层 canonical+验证、适配器 manifest 派生 metadata 与 `plan.Metadata` 等值（`MetadataEquals`）、descriptor pin 三方一致。权威没有重叠区域。

## 5. 三种持久化方案比较与推荐

### 5.1 方案 A：保留 ManifestJson（现状）

SQLite typed Metadata 是领域查询与业务规则的权威；ManifestJson 是 Revision 的不可变交换快照，负责精确导出与往返一致性。

- **维护成本**：零新增组件。canonical 契约仍由单一实现（schema + validator + canonicalizer）承担。
- **重复存储**：每 revision 一份 manifest 文本（现实 1–2 KiB，上限 1 MiB）；revision 不可变、数量有限，代价可忽略。
- **导出确定性**：最强。导出写的是**当初导入的 manifest 原字节**（golden 测试断言逐字节相等），import→export round-trip 字节等价是事实而非推论。
- **未来扩展**：schema 若新增 typed 尚未覆盖的交换字段，快照天然承载，无丢失风险。

### 5.2 方案 B：删除 ManifestJson，全靠 typed 重建

审计结论（§3）表明当前 schema 下技术上可行：常量 + typed 列 + canonical `ParametersJson` 足以重建，经 RFC 8785 归一化后与快照等值。但：

- **维护成本**：需要一个新的 "manifest 重建 writer" 组件——schema 的第二套实现。它必须与嵌入式 schema、`ValidateManifest`、typed 列映射三者永久同步；任何一处漂移都直接破坏 round-trip 字节等价。这正是 ME-05-12 §2/§3 选单一 canonical 契约要避免的漂移面。
- **重复存储**：省 1–2 KiB/revision。
- **导出确定性**：从"字节相同"退化为"字节相同（由重建器正确性保证）"——等价但证明责任转移到一个新组件及其测试。
- **未来扩展**：每加一个 manifest 字段都要同步改重建器；扩展点 `parameters` 必须维持 canonical 字节形式（修复后已是）。

### 5.3 方案 C：最小化 Snapshot

typed Metadata 负责领域字段，快照只保留 typed 无法表示的字段。审计结论：当前 schema 下 typed 无法表示的字段集合为**空**（常量可重建，扩展点已 typed 化），方案 C 退化为 B。它只在一种情形下有意义：未来 schema 引入"交换规范要求但拒绝 typed 化"的字段。作为前瞻条款成立，作为今日方案没有内容。

### 5.4 推荐：A（保留 ManifestJson，现状不变）

1. **规范角色正确**：ME-05-12 §6 明确 package 是交换工件、SQLite 是权威。存储的 canonical manifest 就是交换快照的权威副本——导入存原字节、导出写原字节，职责闭环。
2. **B 的收益不抵成本**：省 1–2 KiB/revision，换来一个必须永久与 schema/validator/typed 映射同步的第二实现。
3. **决策可逆**：A → B/C 随时可做（typed 列权威地位不变，将来加一个迁移删列 + 重建器即可）；B → A 不可逆（原字节已丢）。保留快照是保留选项。
4. **诊断价值**：`manifest_json` 是唯一能不经应用代码直接查看完整 manifest 的途径；导出 fail-closed（`WVPK_EXPORT_MANIFEST_NOT_CANONICAL`）也依赖它的存在作为对照物。
5. **typed 投影已是读权威**：不存在"双权威打架"；若担心漂移，写路径 `MetadataEquals` 已把两者钉死在发布事务内。

## 6. 八项专项风险检查结论

| # | 风险 | 结论 |
|---|---|---|
| 1 | JSON 数值与 .NET 数值精度损失 | manifest 数字全部是整数且 ≤ 2^53−1（schema pin + JCS I-JSON 双重约束）；typed `Int64` 列无损。`origin/spacing` 是 canonical **decimal 字符串**，typed 列按字符串原样存储（有专项测试 `PublishRevision_PreservesExactDecimalStringPrecision`，29 位小数无损）。唯一自由数字区（codec parameters）修复后存 canonical 原字节，词法无损。legacy canonicalizer 曾会把超 Int64 整数坍缩成 double（精度破坏）——字段 JSON 已不再经过它（§4.1/4.2 修复） |
| 2 | RFC 8785 JCS | 存储侧：ManifestJson = canonical 原字节（导入路径 byte-identity 由 reader/validator 保证，服务层再 canonicalize 幂等）；typed 侧修复后 parameters 同为 canonical 字节。曾存在三套 JSON 写出契约（JCS / STJ 默认 / legacy CanonicalJson）混用于同一份数据的缺陷——本次收敛为字段 JSON 全走 JCS（provenance 除外，其契约本来就不同且是内部数据） |
| 3 | Array 顺序的规范意义 | **有规范意义且已保留**：`planes[]` 声明序 = payload 字节布局序（`Ordinal` 列，读路径校验连续性）；`codec_registry[]` 序保留（`Ordinal`）；JCS 不排序数组，canonical manifest 的数组序 = 原序。`category_codes` 是对象（键序经 JCS 排序、语义上无序），行存储 + 读回按 key Ordinal 排序，与 canonical 拼写一致且语义无关紧要 |
| 4 | Nullable / 缺省 / 显式 null | schema 对可空字段（`unit`、`vertical_reference_id`、`mask_plane_name`、`category_codes`）用**缺席**表达，`type` 约束使显式 `null` 非法——不存在三态歧义。typed 投影映射缺席→C# null，重建 null→缺席，一一对应 |
| 5 | Extension 字段在 typed projection 中丢失 | 不会。schema `additionalProperties: false` 全树 closed-world，未知字段在验证层即被拒绝（`RequireProperties`/schema）；唯一扩展点 `codec_registry[].parameters` 以 `ParametersJson` typed 化保存（修复后为 canonical 原字节）。**该表无任何"只存在于 JSON、typed 丢失"的字段** |
| 6 | Descriptor / Spatial Reference 未来变化 | 修订版快照自描述：descriptor pin（key@version+semantics_id）与 SR 绑定 ID 全部在 typed 列中，重建不依赖 registry/SR 行的当前状态。两个开放问题见 §4.4（读路径 registry 校验使旧 revision 依赖 registry 演进策略）与 SR 定义本身不可变（CD-02 既有决策）。ManifestJson 快照在此之上额外保留了精确词形，是纯增益 |
| 7 | Exporter 是否真用权威 Revision 数据 | 是。`FindFieldRevision`（SQLite）→ typed `Metadata`（无则 fail-closed）→ `FieldPayloadReader`（读前验证 hash+length）→ writer 只接收 manifest 字节与 canonical tile 序列；从不触碰原导入包文件。Task 005 已有测试固化（不改 current pointer、重复导出确定性、round-trip 逐字节一致） |
| 8 | ContentFingerprint 与 Manifest 语义变化一致性 | fingerprint = 内部 payload SHA-256（语义 = "内容指纹"，不含 manifest 是**设计**）。manifest↔payload 为结构性绑定（长度对账）非密码学绑定；manifest 单独变化（如 profile_id 改名）不影响 fingerprint 也**不应**影响（它不改变内容）。缺陷面在 §4.3：无持久化 manifest digest → 列篡改读路径不检测。列为决策项，不构成本方案的正确性问题 |

## 7. 对 Domain / Application / EF Mapping / SQLite schema 的修改结论

- **Domain**：本次修复改了一处（`FieldRevisionMetadata.CanonicalizeJson` → canonical 原字节）。无 API/记录形状变化。
- **Application**：零修改（Importer/Exporter 行为与错误码不变）。
- **Infrastructure**：本次修复改了两处 canonical 固定点检查的 canonicalizer 选择。无端口变化。
- **EF Mapping / SQLite schema**：**不需要任何修改**。`manifest_json TEXT NOT NULL` 保留（方案 A）；`parameters_json TEXT NOT NULL` 保留（存储形式约定从"STJ 形式"改为"canonical 原字节"，属内容契约而非 schema）；无新列、无迁移。若未来采纳 §4.3 的 digest 绑定或 §4.4 的 registry 演进决策，再另行提 CD-02 schema 变更。

## 8. 测试补充建议

已实施（本次）：
- `PublishRevision_AcceptsJcsCanonicalManifestWithJcsOnlyStringSpellings`——钉死 §4.1（`+`/非 ASCII manifest 字符串可发布可读回）。
- `PublishRevision_KeepsExponentFormCodecParametersByteIdenticalAndReadable`——钉死 §4.2（指数数字 parameters 字节级保留且 revision 可读）。
- 回归有效性已用"回退→红 / 恢复→绿"双跑证明。

建议（未实施，留后续任务）：
1. **导出篡改检测测试**：直接 SQL 改 `manifest_json` 为另一份合法 canonical manifest 后导出，断言 fail-closed（writer 自校验/对账拦截）——把 §4.3 的间接防线变成显式测试证据；若测试发现拦截不严，升级为缺陷修复。
2. **若产品采纳 manifest digest 列**：补密码学绑定正/负向测试（列 digest 与 `manifest_json` 实际 SHA-256 不符时读路径/导出 fail-closed）。
3. **若未来转 B/C**：先补 "typed 重建 manifest == 存储快照" 逐字节等价测试作为前置证明（本报告 §5 已论证可行性，但该测试是方案切换的验收门槛）。

## 9. 需要 GitHub Actions（macOS）验证的内容

纯审计 + 两处检查/存储形式修复 + 2 个回归测试：macOS Debug/Release 构建 + 608 测试全绿即可。无平台相关新路径（两处修复不涉及文件系统/字节序/编码平台差异；`FieldManifestCanonicalizer` 已在 macOS CI 覆盖范围内）。

## 10. 明确需要产品决策的事项

1. **manifest 完整性绑定**（§4.3）：是否为 revision 增加 `manifest_canonical_sha256` 持久化 digest，使读/导出路径能显式检测 `manifest_json` 列级篡改？涉及 CD-02 schema 变更。V1 单机威胁模型下可不做。
2. **registry 演进策略**（§4.4）：`FieldDescriptorRegistry` 是否规则化为"只增不删/只增不改"，保障已发布 revision 永远可读？属 Domain 演进策略，建议落一条 ADR 级决定。
3. **生产 profile / codec 决策**（ME-05-12 §4 既有事项，本审计再次确认不受影响）：精度/类别码/codec/tile 尺寸仍是待决产品参数；本审计未引入任何默认值。
4. **Provenance 是否记录来源包指纹**（005 报告遗留事项 4）：与本审计相关联——若需要"来源包 digest"一等元数据，同样是新列决策；当前 `ManifestJson`（原字节）+ 结构性 payload 绑定已覆盖 round-trip 需求。

## 11. Windows 构建与测试结果

```text
dotnet build WorldView.sln -c Debug   → 0 错误
dotnet test  WorldView.sln -c Debug   → 608/608 通过（Domain 223 + Application 118 + Infrastructure 267）
dotnet build WorldView.sln -c Release → 0 错误
dotnet test  WorldView.sln -c Release → 608/608 通过
# 回归有效性双跑（临时回退修复 → 2 失败 / 恢复 → 全绿）
```

相对 005 基线（606）新增 2 个测试，均在 `Cd02FieldDatasetPersistenceTests`。

## 12. 修改文件清单

| 文件 | 变更 |
|---|---|
| `WorldView/src/WorldView.Domain/Fields/FieldRevisionMetadata.cs` | 修复：`CanonicalizeJson` 改为 RFC 8785 canonical 原字节（codec parameters 存储形式） |
| `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/SqliteCoreDomainDatabase.Fields.cs` | 修复：manifest 与 codec parameters 两处固定点检查改用 `FieldManifestCanonicalizer`（附契约注释） |
| `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/Cd02FieldDatasetPersistenceTests.cs` | 新增 `ManifestWithExtraCodec` helper + 2 个回归测试 |
| `docs/development/local-progress/field-package/006-manifest-persistence-authority-audit.md` | 本报告 |
| `docs/development/local-progress/README.md` | 索引登记 |

## 13. 交付边界

- 本任务**未删除、未降级 ManifestJson**（采纳方案 A，现状权威结构不变）。
- 两处缺陷修复为独立 commit，与本报告 commit 分离。
- 未引入 V1/V2 兼容机制、并发架构、生产精度/量化/codec 默认值；未把 fixture-only profile 升级为 production。
- PR #17 保持不合并。
