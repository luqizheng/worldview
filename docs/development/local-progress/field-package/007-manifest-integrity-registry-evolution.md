# 007 — Manifest 持久化完整性与 Descriptor Registry 演进规则

**日期**：2026-11-23
**分支**：`feat/field-package-reader-writer`（PR #17 head，stacked on PR #11）
**性质**：完整性验证 + 1 处导出防线缺口的最小修复 + 架构规则固化（无 schema 变更、无迁移、无兼容层、无新数据库字段）
**状态**：四平台 CI 全绿（run `45124453470`，head `6f91f6f`）；Windows 本地 Debug/Release 各 **632/632** 通过（608 基线 + 24 新增）
**Commit**：修复 `7729e70`（fix(fields)），报告 `4603802`（docs(local-progress)），CI 验证回写见 §9

## 1. 任务范围与实际完成内容

两个工作项：

- **工作项 A（Manifest 持久化完整性）**：从 Importer 到 Exporter 追踪六类数据身份的权威关系；以真实 SQLite + 触发器旁路注入实现持久化后篡改测试矩阵（7 类情形）；发现导出路径存在一处**语义篡改静默导出缺口**，实施最小修复（导出端重建发布时不变量）；正常 round-trip 不受影响。
- **工作项 B（Descriptor Registry 演进规则）**：审查 Registry 现状（编译期静态映射，无任何运行时覆盖/修改/删除路径）；确认现有架构已通过"编译期映射 + 读路径 pin 重验证 + 数据库触发器"保证 fail-closed 与防静默重解释；按"现有防线优先"原则**不新建 Registry 管理系统**，以验证测试与 ME-05-12 §8 规则文档固化。

## 2. Manifest 完整数据路径（A1）

```
Field Package (WVFLDPK 字节)
  → FieldPackageReader.Open            二进制全量校验；ManifestUtf8 = 包内 canonical manifest 原字节
  → FieldPackageManifestValidator      1 MiB 预算 / 严格 UTF-8 / canonical 字节验证 / 嵌入式 P2-09-34 schema
  → FieldPackageOwnerContextValidator  descriptor identity + Registry pin + FieldDatasetService.ValidateManifest
                                       → typed FieldRevisionMetadata（导入侧投影）
  → FieldPackageImporter               profile gate → 整包 SHA-256 pass → 逐 tile 验证+增量 payload SHA-256 pass
                                       → 内部 payload（decoded tile 串接）组装/流式 staging
  → FieldDatasetService.PublishRevision 发布权威：canonicalize（幂等）→ ValidateManifest → FromValidatedManifest
  → SqliteCoreDomainDatabase.PublishFieldRevision
        manifest 派生 metadata == plan.Metadata（StructurallyEquals）
        descriptor pin 三方一致（manifest/plan/dataset）
        同一事务：revision 行（ManifestJson 原字节 + typed 列）+ planes/categories/codecs 子行
        （ParametersJson = canonical 原字节）+ payload 行 + Dataset current pointer CAS
  → 读取 FindFieldRevision / MapFieldRevision
        完全从 typed 列重建 metadata（零 JSON 解析）；ParametersJson canonical 固定点检查；
        descriptor pin 对 Registry 重验证（fail-closed）；payload 行映射
  → FieldPackageExporter.ExportAsync
        typed Metadata（无则 fail-closed）→ profile gate → payload format V1 检查
        → 持久化 manifest canonical 字节检查 →【Task 007 新增】发布权威一致性重建（见 §5）
        → tile plan（typed）vs payload 字节长度对账 → FieldPayloadReader（hash+length 验证）
  → FieldPackageWriter                 manifest 字节 verbatim + canonical tile 序 → 自校验 → 原子 rename
  → 新 Field Package
```

### 六类数据身份的权威关系（未混淆）

| 身份 | 权威角色 |
|---|---|
| **ManifestJson**（持久化列） | 不可变交换快照：导入包 manifest 的 canonical 原字节；导出时 verbatim 写回；绝不从 typed metadata 重建 |
| **Typed Metadata** | 领域读取与业务操作的唯一权威：读路径从 typed 列重建，零 JSON 解析 |
| **Internal Field Payload** | 运行时 Tile 数据权威：decoded tile 按 canonical 序串接；读取时 hash+length 双验证 |
| **Package SHA256** | 整个 WVFLDPK 交换文件摘要：仅出现在导入/导出结果记录，不进入 payload 元数据 |
| **Internal Payload SHA256** | 内部 payload 摘要 = payload `ContentHash`（元数据角色）= Revision `ContentFingerprint`（内容身份角色），同值双角色 |
| **Revision ContentFingerprint** | Revision 内容身份 = 内部 payload SHA-256（Task 005 §2.2 决策）；不含 manifest——manifest 语义变化（如 profile_id 改名）不改变内容，**不应**影响 fingerprint |

## 3. 篡改测试矩阵（A2）与结果

数据库层已有第一道结构性防线：`trg_core_map_field_revision_immutable_update` 对 `core_map_field_revision` 的**任何** UPDATE/DELETE 直接 `RAISE(ABORT)`（Task 006 报告未记录此防线，本任务补记并有专项测试）。测试矩阵因此先 **DROP 该触发器**，模拟"数据库级防线被完全绕过"的最坏情况（如篡改备份恢复后回灌、触发器丢失），再直接 UPDATE `manifest_json`，然后尝试导出：

| # | 篡改情形 | 结果（全部真实 SQLite） |
|---|---|---|
| 0 | 触发器完好时直接 UPDATE manifest_json | `SqliteException`（结构防线本身，专项测试固化） |
| 1 | 改为另一份合法且不同的 canonical manifest（`spatial_reference_id` → 其他合法值） | `WVPK_EXPORT_MANIFEST_METADATA_MISMATCH` fail-closed；无输出文件；数据库与 payload 原样 |
| 2 | 改为非 canonical JSON（注入空白） | `WVPK_EXPORT_MANIFEST_NOT_CANONICAL`（既有防线） |
| 3 | 改为非法 JSON（截断） | `WVPK_EXPORT_MANIFEST_NOT_CANONICAL`（canonicalizer 把解析失败包成 `CoreDomainDataException`，既有防线） |
| 4 | manifest 字段与 typed 列不一致（`grid.origin_x` "0"→"7"：canonical decimal、布局与 payload 长度均不变） | `WVPK_EXPORT_MANIFEST_METADATA_MISMATCH`（**修复前此情形被静默导出**，见 §4） |
| 5 | codec parameters 数值表示改变：(a) 同值异拼 `1e-7`→`1E-7` (b) 异值同形 `1e-7`→`2e-7`（均需保持 JSON 合法） | (a) `WVPK_EXPORT_MANIFEST_NOT_CANONICAL`；(b) `WVPK_EXPORT_MANIFEST_METADATA_MISMATCH`（codec parameters 按 canonical 字节逐条比对） |
| 6 | manifest 被篡改但 Tile Payload 未动（上述全部情形共有的前提） | 全部 fail-closed；payload 字节/ContentHash/ContentFingerprint 逐字节不变 |
| 7 | 篡改后尝试 Export | 同 1–5；且数据库完全未被触碰：被篡改的 manifest_json 保持原样（**不自动重建掩盖**）、typed 列不变、Dataset current pointer 不变 |

补充覆盖：

- canonical 但不再满足发布权威验证的 manifest（`no_data.mask_plane.valid_value` 0→2）→ `WVPK_EXPORT_MANIFEST_INVALID`（新增错误码）。
- 同库对照：被篡改 revision 导出被拒的同时，同 Map 另一 Dataset（land_water）的正常 revision 仍完成 byte-identical 导出（golden SHA-256 相等）——守卫不污染其他数据。

**测试实现说明**：全部通过 `ISqliteConnectionProvider.GetAccess()` 直连真实 SQLite 执行 `DROP TRIGGER` + `UPDATE`；导出断言 typed `FieldPackageValidationException` 的 `Code`；逐项断言无输出文件、无 tmp 残留、manifest_json 保持被篡改字节（未被重建）、typed 列/pointer/payload 未变。

## 4. 缺口与最小修复

### 4.1 【缺陷·已修复】导出路径对"布局保持型语义篡改"静默放行

- **位置**：`FieldPackageExporter.ExportAsync`（修复前仅有 canonical 字节检查 + writer 重验证 + 布局对账）。
- **现象**：三类防线只能拦截**非 canonical、schema 非法、破坏 tile 布局**的篡改。若篡改后的 manifest 仍是 canonical、schema 合法、且 tile 布局与 typed 派生计划一致（如改 `spatial_reference_id`/`map_space_id`/`profile_id`/`unit`/`origin_x`/category codes/非零 codec 参数值），导出**成功**并把这些与权威 typed 列矛盾的字节 verbatim 写入交换包——不一致的 Revision 被静默导出。红跑实证：禁用守卫后 4 个语义篡改测试全部"静默成功"。
- **修复**（最小范围，无 schema/迁移/哈希列）：
  1. `FieldRevisionMetadata.StructurallyEquals`（Domain）：公开的结构等值契约——typed 列逐成员 + planes/categories/codecs 逐元素（codec parameters 按 canonical 字节序数比较）。Infrastructure 原私有 `MetadataEquals` 逻辑**原样上移**为该唯一契约（发布路径行为不变）。
  2. `FieldPackageExporter` 步骤 4b：用**发布同一权威** `FieldDatasetService.ValidateManifest`（以 typed pin 调用）重建 manifest 投影，再与 typed `Metadata` 做 `StructurallyEquals`。失败 → `WVPK_EXPORT_MANIFEST_INVALID` / `WVPK_EXPORT_MANIFEST_METADATA_MISMATCH`。
- **不做的事**：不重建 ManifestJson 来掩盖篡改；不在读路径加 JSON 解析（Task 006 §4.5 的零解析设计保持）；不加 manifest hash 列（任务明确禁止作为默认方案）；不引入历史兼容层。
- **验证**：红/绿双跑——禁用守卫恰 5 个测试失败（4 语义篡改静默导出 + 1 对照），恢复守卫全绿；证明测试钉住的是新防线而非恒真。

### 4.2 现有防线清单（Exporter 完整性防线全景，修复后）

| 防线 | 拦截对象 | 错误码 |
|---|---|---|
| SQLite 触发器（第一道，结构级） | 任何 SQL 层 UPDATE/DELETE published revision；Dataset pin 原地修改 | `SqliteException` |
| canonical 字节检查 | 非 canonical / 非法 JSON / 异值异拼 | `WVPK_EXPORT_MANIFEST_NOT_CANONICAL` |
| 发布权威重验证（**本次新增**） | canonical 但不再满足闭世界语义验证 | `WVPK_EXPORT_MANIFEST_INVALID` |
| manifest↔typed 结构等值（**本次新增**） | 合法但与 typed 列不一致（含 codec parameters 异值） | `WVPK_EXPORT_MANIFEST_METADATA_MISMATCH` |
| payload format 检查 | 非 Internal Payload V1 | `WVPK_EXPORT_PAYLOAD_FORMAT` |
| tile plan ↔ payload 长度对账 | 布局与内容长度矛盾 | `WVPK_EXPORT_PAYLOAD_LENGTH` |
| `FieldPayloadReader` | payload hash/length 不符 | 读前验证失败 |
| Writer 自校验 | 产出包与 manifest/目录不一致 | 包层错误码 |
| Profile Gate | fixture-only revision 洗白 | `WVPK_POLICY_GOLDEN_FIXTURE_ONLY` |

**残留边界（设计如此，非缺陷）**：数据库文件离线字节级改写（绕过 SQL 层）仍只能在导出时被以上 typed 对账拦截，读路径按设计零 JSON 解析不重验 manifest。若未来威胁模型要求读路径显式检测，需要 `manifest_canonical_sha256` 列（Task 006 §4.3 遗留产品决策，本任务按任务书未采纳）。

## 5. Descriptor Registry 现状与固化规则（B1/B2）

### 5.1 现状审查结论

- `FieldDescriptorRegistry` 是**编译进程序集的纯函数**：`(field_key, schema_version) → semantics_id` switch；V1 注册 `elevation@1`、`land_water@1`。**不存在**任何运行时覆盖、原地修改、删除或热加载路径——没有 Registry 管理系统可被滥用。
- 全部消费点均 fail-closed：发布（`PublishRevision`/`ValidateManifest`：未注册或 semantics 不符 → `CoreDomainConflictException`）、导入预检（OwnerContextValidator）、读路径（`MapFieldDataset`/`MapFieldRevision`：pin 与 Registry 不符 → `CoreDomainDataException`，行不可静默重解释）。
- 数据侧 pin 双重保护：发布事务内 pin 不可变检查（适配器）+ `trg_core_map_field_dataset_pin_immutable` 触发器；revision 行整体不可变。
- **结论**：现有架构已满足 B2 基线 7 条规则的全部行为要求。按任务书"现有防线优先"，**只增加验证测试与文档，不重复实现 Registry 管理系统**。

### 5.2 不可变语义 vs 非语义展示元数据

V1 Registry **只含语义映射，不存在展示元数据字段**——整个映射（`(field_key, schema_version) → semantics_id`）的每个字段都是不可变语义。不存在"整个 JSON 文件不可修改"的判定问题（Registry 不是 JSON 文件）。规则上明确：若未来引入展示元数据，必须放在**独立的、显式非语义的结构**中，绝不并入语义映射、绝不影响 descriptor 解析。

### 5.3 正式演进规则（已固化至 ME-05-12 §8.3）

1. 已发布映射**永久不可变**（读路径从 Registry 重导出 pin——改映射即让全部派生 revision fail-closed）。
2. 已发布映射**不可删除**（删除即让既有 Dataset/Revision 不可读）。
3. 语义变化只通过**新增** `(field_key, schema_version)` + 新 `semantics_id`（约定 `world-view.field.<key>.v<N>`）。
4. 新版本不得改变旧版本解释语义。
5. Revision 永远使用其原始 typed pin；发布期 pin 不可变 + 读期对 Registry 重验证。
6. 不可能通过更新 Registry 静默改变已有 Revision 含义（结构上不可能 + 测试钉住）。
7. 新增是唯一允许的演进，且不得影响既有条目。

## 6. 新增测试（24 个）

| 文件 | 新增 | 覆盖 |
|---|---|---|
| `FieldPackageImportExportAuthorityTests.cs` | +9（10→19） | 触发器防线专项 1；篡改矩阵 7（§3 编号 1–5、7 与补充情形）；同库对照 round-trip 1 |
| `Cd02FieldDatasetPersistenceTests.cs` | +5（63→68） | revision `semantics_id`/`schema_version` 篡改读路径 fail-closed 2；dataset pin 篡改 fail-closed 1；dataset pin 触发器防线 1；双 descriptor 同 Map 共存 1 |
| `FieldDescriptorRegistryTests.cs`（新文件） | +6 | 已注册映射确定性解析；未注册 key/version/key 空串 fail-closed ×4（Theory）；加性共存独立性 |
| `FieldRevisionMetadataStructuralEqualityTests.cs`（新文件） | +4 | 独立构建等值；null/异 manifest 不等；全部语义成员参与等值 ×16 断言；集合成员逐元素（fresh 实例等值 / plane 重排不等 / codec parameter 字节变化不等） |

## 7. 修改文件清单

| 文件 | 变更 |
|---|---|
| `WorldView/src/WorldView.Domain/Fields/FieldRevisionMetadata.cs` | 新增 `StructurallyEquals`（唯一结构等值契约，附 XML 文档说明其双重用途） |
| `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/SqliteCoreDomainDatabase.Fields.cs` | 发布路径 `MetadataEquals` 私有逻辑替换为 `StructurallyEquals` 调用（行为不变，逻辑上移） |
| `WorldView/src/WorldView.Application/Fields/FieldPackageExporter.cs` | 新增 2 个错误码（`WVPK_EXPORT_MANIFEST_INVALID` / `WVPK_EXPORT_MANIFEST_METADATA_MISMATCH`）+ 步骤 4b 发布权威一致性守卫 `RequireManifestConsistentWithTypedMetadata` + 类注释补充 |
| `WorldView/tests/.../FieldPackageImportExportAuthorityTests.cs` | 新增篡改段落（helpers + 9 测试，见 §6） |
| `WorldView/tests/.../Cd02FieldDatasetPersistenceTests.cs` | 新增 Registry 演进段落（`DropTrigger` helper + 5 测试） |
| `WorldView/tests/WorldView.Domain.Tests/Fields/FieldDescriptorRegistryTests.cs` | 新文件 |
| `WorldView/tests/WorldView.Domain.Tests/Fields/FieldRevisionMetadataStructuralEqualityTests.cs` | 新文件 |
| `docs/map-editor/ME-05-12-field-package-v1-implementation-decisions.md` | 新增 §8（权威关系表 / 导出完整性守卫 / Registry 演进规则） |
| `docs/development/local-progress/README.md` | 登记本报告 |
| `docs/development/local-progress/field-package/007-manifest-integrity-registry-evolution.md` | 本报告 |

**未修改**：SQLite schema/Migration、Field Payload 格式、Field Package 二进制格式、ManifestJson 的存在与角色、Fixture Profile、Importer 行为与错误码。

## 8. Windows 构建与测试结果

```text
dotnet build WorldView.sln -c Debug   → 0 错误（警告为既有类别，无新增）
dotnet test  WorldView.sln -c Debug   → 632/632 通过（Domain 233 + Application 118 + Infrastructure 281）
dotnet build WorldView.sln -c Release → 0 错误
dotnet test  WorldView.sln -c Release → 632/632 通过
# 回归有效性红/绿双跑：禁用守卫 → 恰 5 失败（语义篡改静默导出实证）；恢复 → 全绿
```

相对 006 基线（608）新增 24 个测试（§6）。

## 9. macOS CI 状态：已验证（2026-11-23 回写）

**结果**：临时 CI 验证 PR #21（base=master、head=`feat/field-package-reader-writer` @ `6f91f6f`，body 注明勿合并）触发 run **`45124453470`**，四个 job 全部 success：

| Job | 结果 |
|---|---|
| windows-latest / Debug | success |
| windows-latest / Release | success |
| macos-latest / Debug | success |
| macos-latest / Release | success |

Task 005/006/007 的 macOS Debug/Release 验证至此完成；Windows 侧另有本地全量实证（§8）。

**根因（保留记录）**：PR #17 的 base 是 `feat/efcore-v1-complete-model`（PR #11，未合并），而 `dotnet-ci.yml` 仅对 `pull_request: branches: [master]` 与 `push: master` 触发。因此 Task 005/006 的四个提交（`4f9427c`、`12e443d`、`0831f88`、`47e9159`）以及 `7729e70`、`4603802` 均未产生 workflow run；本次通过临时 PR #21 恢复验证。

**后续影响**：只要 PR #17 仍以 PR #11 分支为 base，任何新推送都不会自动触发 CI；后续任务需继续沿用临时 CI PR 或调整 workflow 触发条件（CI 配置变更留待用户裁决）。

## 10. 未解决问题与后续依赖

1. **macOS CI 触发机制**（§9）：Task 007 本身已通过临时 PR #21（run `45124453470`）验证；后续 stacked PR 的自动触发仍需要用户决策——继续使用临时 CI PR 或调整 workflow 触发条件。
2. **读路径 manifest 密码学绑定**（Task 006 §4.3 遗留）：本任务继续未采纳 digest 列；威胁模型升级时另行提案。
3. **Registry 演进规则的未来扩展**：新 schema_version 注册、展示元数据结构（若引入）均需按 ME-05-12 §8.3 走正式变更；当前无待办条目。
4. 生产 profile/codec/精度决策（ME-05-12 §4 既有事项）不受本任务影响。

## 11. 交付边界

- PR #17 保持不合并；未开始 Task 008。
- 未引入并发机制、生产默认值、兼容层；未把 fixture-only profile 升级为 production。
- 修复 commit 与报告 commit 分离（同 Task 006 惯例）。
