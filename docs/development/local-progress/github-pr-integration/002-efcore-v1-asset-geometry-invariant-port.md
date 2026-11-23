# Task 002 — EF Core V1 Asset / Geometry Invariant Port

- 分支：`feat/efcore-v1-asset-geometry-invariants`
- Base：`feat/efcore-v1-complete-model`（PR #11 Head，Draft 保持）
- Stacked PR：[#22](https://github.com/luqizheng/worldview/pull/22)（Draft，Base 指向 PR #11 Head）
- 基线 SHA：`991aa17b7a3f6f2588aabf596e2e808b6c9cdde2`
- 实现提交 SHA：`d017449ab78f041eda2a612eb3f5c67bf9d61b92`
- Task 002-A 修复提交 SHA：见 §11（与文档同批提交后由 CI run 记录）
- 状态：本地 Windows Debug/Release 全部通过；四平台 GitHub Actions 全部成功

## 0. Task 002-A 修复记录 — Verified 写入不可绕过 + `$defs` 递归 fail-closed

### A1. 原缺陷与风险

- **Asset Verified 可绕过真实字节验证**：`AssetVerificationEvidence(ExpectedHash, ActualHash, ExpectedByteLength, ActualByteLength)` 是公开可构造 DTO。调用方只需自行构造四个与数据库元数据相等的值，即可直接调用 `MarkAssetVerified` 获得 `Verified` 状态——完全不需要真实读取 Asset 字节。G1 的 Verified-only 引用与 G3 的发布事务重检查都建立在 `Verified` 之上，伪造 `Verified` 即可让损坏/缺失字节进入正式 Geometry 引用。
- **`$defs` 内部未做关键字 fail-closed 检查**：`EnsureSupportedKeywords` 支持 `$defs` 关键字本身，但不递归遍历其内部定义。`$defs` 中（无论是否被 `$ref` 引用）出现任何不支持关键字都会被静默忽略——违反 fail-closed 原则；未引用定义中的未知关键字会长期潜伏，直到后续 schema 演进引用该定义时才暴露语义漂移。

### A2. 修复方案

**A 部分（不可伪造验证凭证）**：Verified 写入能力收敛到真实字节验证流程，采用**不透明实测摘要凭证**设计（分层不变：Domain 不依赖 Application/Infrastructure）：

- 新增 Domain 类型 `AssetByteDigest`（`WorldView.Domain/Assets/AssetByteDigest.cs`）：私有构造器 + 唯一公开静态工厂 `MeasureAsync(Stream, CancellationToken)`——完整读取真实字节流并现场计算 SHA-256（小写十六进制）与实际长度。构造器对 Domain 程序集外不可见，普通调用方**不经真实字节读取就不存在摘要实例**（对元数据 hash 逆像是不可行的，摘要只能来自确实存在的字节）。
- `VerifyAssetIntegrityUseCase`：`IAssetStore.OpenReadAsync` 打开真实文件后改由 Domain 的 `MeasureAsync` 测量（哈希计算职责移入 Domain，与 `AssetRules.Sha256Hex` 同层）；比对元数据后以摘要调用 `MarkAssetVerified`。
- `ICoreDomainDatabase.MarkAssetVerified` 签名改为 `(projectId, assetId, AssetByteDigest digest, nowUtcMs)`；SQLite 实现仍在**同一写事务内**重读当前 `core_asset` 行，要求 digest 的 hash/byte_length 与当前 `content_hash`/`byte_length`（及 `sha-256` 算法声明）完全一致，不一致抛 `CoreDomainConflictException`——事务保护不降反升（旧接口还接受调用方自报的 expected 值，新接口只信事务内重读 + 实测摘要）。
- `RecordAssetIntegrityFailure` 仍只接受 `Corrupt/Missing`；验证失败（Corrupt/Missing/ReadFailed→Corrupt）照旧覆写旧 `Verified`（过期验证失效）。
- `Missing / Corrupt / ReadFailed / AssetNotFound` 结果区分保持不变；`ReadFailed` 不会被静默转换为验证成功。

**B 部分（`$defs` 递归 fail-closed）**：`EnsureSupportedKeywords` 新增 `$defs` 分支——对每个定义递归执行关键字检查；未引用定义、被 `$ref` 引用定义、嵌套 `$defs` 全部覆盖，因此 `$ref` 解析目标必然已经过完整关键字支持检查。Draft 2020-12 子集边界不变（未扩大支持面）；未支持关键字抛 `RegistryException("schema_invalid")`。

### A3. API 变化

| 变化 | 旧 | 新 |
|---|---|---|
| Domain DTO | `AssetVerificationEvidence`（公开 record，4 个可伪造字段） | 删除；以 `AssetByteDigest`（私有构造器 + `MeasureAsync`）取代 |
| 端口签名 | `MarkAssetVerified(projectId, assetId, AssetVerificationEvidence, nowUtcMs)` | `MarkAssetVerified(projectId, assetId, AssetByteDigest, nowUtcMs)` |
| Verified 接受条件 | evidence 内部 expected==actual 且 expected 匹配当前元数据 | digest 与事务内重读的当前元数据完全一致（digest 只能来自真实字节测量） |
| 哈希计算归属 | Application（UseCase 私有 `ComputeHashAsync`） | Domain（`AssetByteDigest.MeasureAsync`，与 `AssetRules.Sha256Hex` 同层） |

对正常调用方（走 `VerifyAssetIntegrityUseCase`）无源代码级破坏；直接构造 evidence 的调用方将编译失败（预期效果——该路径本就应被关闭）。

### A4. 新增测试

- `WorldView/tests/WorldView.Domain.Tests/Assets/AssetByteDigestTests.cs`（新增 5 项）：
  - 实测字节得到正确 SHA-256 与长度；
  - 跨多缓冲区大流分块读取不丢块；
  - 空流 → sha-256(empty)/0；
  - null 流 → `ArgumentNullException`；
  - 防伪造 API 面：无公开实例构造器，公开静态成员仅 `MeasureAsync`。
- `WorldView/tests/WorldView.Infrastructure.Tests/Assets/AssetIntegrityVerificationTests.cs`（重构 + 新增）：
  - `MarkVerified_WithDigestMeasuredFromOtherBytes_RejectedAndStateUnchanged`：真实测量但元数据不匹配 → 拒绝写入、状态不变（替代旧的伪造 evidence 用例——旧用例本身演示了缺陷）；
  - `VerifiedWrite_ApiSurfaceOnlyAcceptsMeasuredDigest`：端口签名 + 摘要类型 API 面锁定（普通调用方不能伪造证据、不经真实字节读取不能写入 Verified）；
  - 原有 G2 全链路（真实字节 Verified / Missing / Corrupt / ReadFailed / 篡改后过期验证失效 / Verified-only 引用）继续有效。
- `WorldView/tests/WorldView.Domain.Tests/Registry/JsonSchemaSubsetTests.cs`（新增 4 项）：
  - `$defs` 未引用定义含未知关键字 → `schema_invalid` 拒绝；
  - `$defs` 被 `$ref` 引用定义含未知关键字 → 拒绝；
  - 嵌套 `$defs` 含未知关键字 → 拒绝；
  - 合法 `$defs` + `$ref` 组合校验（引用约束真实生效）。
- 真实 Building/Cave schema（各含 22/14 个 `$defs` 定义、数十处 `$ref`）在新递归检查下继续通过原有正反例测试。

### A5. Task 002-A 本地结果（Windows）

| 步骤 | 结果 |
|---|---|
| Debug Build | 通过（0 error） |
| Debug Test | 通过 537 / 失败 0（原 527 + 新增 10） |
| Release Build | 通过 |
| Release Test | 通过 537 / 失败 0 |

### A6. Task 002-A 四平台 CI

见 §11（与修复代码同批推送后由对应 CI run 记录准确 HEAD SHA）。

## 1. 实际迁移的领域规则

### G1 — Asset Integrity

- 正式 Geometry Definition 仅可引用 `Verified` Asset；`Unverified` 与 `Corrupt/Missing` 均拒绝建立正式 typed link。
- 唯一可写入 `Verified` 的路径为 `MarkAssetVerified(digest)`，且持久化适配器在写事务内重读当前 Asset 元数据，要求实测摘要与当前 `content_hash`/`byte_length` 完全一致（Task 002-A 后 digest 只能来自真实字节流测量，公开 DTO 伪造路径已关闭）。
- `RecordAssetIntegrityFailure` 只接受 `Corrupt/Missing`，从接口层杜绝调用方直接写 `Verified`。
- `PublishObjectDefinitionWithGeometryAssets` 在 SQLite 事务内重读 Object 生命周期、Definition CAS、Asset `content_hash` 与 `integrity_state`。
- `LinkDefinitionAsset` 的正式引用错误与 `Publish...` 的失败均以明确异常返回；发布事务失败整体回滚，不留部分 Definition 或 Link。

### G2 — Asset Byte Verification

- 新增 Domain 不透明凭证 `AssetByteDigest`（Task 002-A 取代最初的公开 `AssetVerificationEvidence` DTO）：唯一产生途径 `MeasureAsync` 读取真实字节流并计算 SHA-256 与长度。
- 新增 Application 用例 `VerifyAssetIntegrityUseCase`：
  - 通过 `IAssetStore.OpenReadAsync` 读取真实不可变字节；
  - SHA-256 与长度均由实际字节流计算（经 Domain `MeasureAsync`，哈希计算与 `AssetRules` 同层）；
  - 结果区分 `Verified / Missing / Corrupt / ReadFailed / AssetNotFound`；
  - 失败写入 `Corrupt` 或 `Missing`，不会保留旧 `Verified`；
  - 同一内容寻址文件被篡改后再次验证会重新计算并转为 `Corrupt`（过期验证结果失效）。
- 测试使用真实临时 SQLite 与真实临时文件（含文件锁模拟读取失败），不 mock hash 计算。

### Geometry Manifest Identity — PR #5

- `GeometryRules.ValidateManifest` 新增 `Guid.Empty` 拒绝（`geometry_component_id`、`asset_id`）。
- 新增 `GeometryRules.ValidateManifestCollection`：
  - 全量执行 role/family 校验；
  - Descriptor role `min` / `max` 基数；
  - descriptor `combined_min`；
  - owner 内 `geometry_component_id` 唯一性。
- 复杂集合规则保留在 Domain；SQLite 仍负责 FK/UNIQUE/事务约束。
- 通过 G3 原子发布与 `EditDefinition` 对 `geometry_components` 的禁止，阻止部分更新绕过完整集合验证。

### Conditional JSON Schema — PR #6

- `JsonSchemaSubset` 新增 `if / then / else / not`，遵循 Draft 2020-12 语义：
  - `if` 仅作条件判定，不传播条件分支违规；
  - 命中应用 `then`，未命中应用 `else`；
  - `not` 在子 schema 满足时产生违规。
- 新增关键字进入 `SupportedKeywords`，条件子 schema 递归执行未知关键字 fail-closed 检查；未知关键字不会被静默视为成功。
- 明确文档边界：仅支持记录在案的 JSON Schema 子集，不宣称完整标准支持。
- 新增真实 Building/Cave payload 的 schema 与条件规则测试。

### G3 — Building / Cave Atomic Publication

- 新增 Domain 计划 `ObjectDefinitionPublicationPlan` 与 `ObjectDefinitionService.PublishDefinitionWithGeometryAssets`。
- 新增 `ICoreDomainDatabase.PublishObjectDefinitionWithGeometryAssets`，在**单个 EF Core 事务**内：
  1. 重读 Object 生命周期（仅 active）与 Object Type；
  2. 重读 Definition 行并校验 `definition_hash == SHA256(payload)` 的数据一致性；
  3. Compare-And-Swap：expected hash 必须等于当前 hash；
  4. canonicalize + typed schema 校验；
  5. 校验既有 typed link set 与既有 payload 的集合一致性；
  6. 从事务内 `core_asset` 行重读 `content_hash` 与 `integrity_state`，仅 `Verified` 进入正式集合；
  7. 用完整集合一致性校验（`BuildingCaveAggregateValidator`）验证 owner payload ↔ desired links ↔ asset catalog；
  8. 全量替换 `geometry_component` links 并更新 Definition；
  9. 任一步异常回滚整个事务。
- `LinkDefinitionAsset` / `UnlinkDefinitionAsset` 对 Building/Cave typed aggregate 直接拒绝；`EditDefinition` 禁止改变 `geometry_components`；`InsertObject` 要求 Building/Cave 初始 `geometry_components` 为空。
- `BuildingCaveAggregateValidator` 发布上下文补齐：
  - Asset ID 大小写归一化；
  - 空 link set 不再绕过集合一致性；
  - 缺失 Asset、重复 link、非法 link role、重复 Asset 目录项、组件 hash 冲突的明确错误码。

## 2. 旧 PR 迁移情况

| 旧 PR | 分支 | 迁移情况 |
|---|---|---|
| #1 G1 Asset Integrity | `fix/cd-02-g1-asset-geometry-integrity` | 已迁移到 EF Core；Verified-only 引用与发布事务重检查已落实。 |
| #2 G2 Byte Verification | `feat/cd-02-g2-asset-byte-verification` | 已迁移真实字节验证用例与 Verified 写入收敛（Task 002-A 后为不可伪造 `AssetByteDigest` 凭证）、`RecordAssetIntegrityFailure`。 |
| #5 Manifest Identity | `fix/cd-02-g3-geometry-manifest-identities` | 已迁移空 ID 校验与 `ValidateManifestCollection`（min/max/combined_min/唯一性）。 |
| #6 Conditional Schema | `feat/json-schema-conditional-keywords` | 已迁移 `if/then/else/not` 与 fail-closed 扩展，并补充真实 Building/Cave 测试。 |
| #7 Atomic Publication | `feat/cd-02-g3-atomic-geometry-publication` | 语义已迁移并重新实现为 EF Core 单事务版本；未恢复旧 SQLite Adapter。 |

旧分支与当前领域模型未发现需要改变已确认语义的冲突。旧 PR 均未合并、未关闭、Base 未修改。

## 3. 新增 / 修改文件

### Domain

- `WorldView/src/WorldView.Domain/Assets/AssetRecords.cs`（Task 002-A 删除公开 `AssetVerificationEvidence`）
- `WorldView/src/WorldView.Domain/Assets/AssetByteDigest.cs`（Task 002-A 新增：不可伪造实测摘要凭证）
- `WorldView/src/WorldView.Domain/Assets/AssetService.cs`（Verified-only 引用；Building/Cave 禁止单条 link/unlink）
- `WorldView/src/WorldView.Domain/Aggregates/BuildingCaveAggregateValidator.cs`（发布集合严格一致性）
- `WorldView/src/WorldView.Domain/Geometry/GeometryRules.cs`（空身份 + `ValidateManifestCollection`）
- `WorldView/src/WorldView.Domain/Objects/ObjectPlans.cs`（`ObjectDefinitionPublicationPlan`）
- `WorldView/src/WorldView.Domain/Objects/MapInitialStateService.cs`（原子发布服务；`EditDefinition` 组件变更保护）
- `WorldView/src/WorldView.Domain/Objects/ObjectCreationService.cs`（Building/Cave 初始组件必须为空）
- `WorldView/src/WorldView.Domain/Persistence/ICoreDomainDatabase.cs`（验证凭证 API；原子发布 API）
- `WorldView/src/WorldView.Domain/Registry/JsonSchemaSubset.cs`（条件关键字；Task 002-A `$defs` 全量递归 fail-closed）

### Application

- `WorldView/src/WorldView.Application/UseCases/VerifyAssetIntegrityUseCase.cs`（新增）

### Infrastructure

- `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/SqliteCoreDomainDatabase.Assets.cs`（证据驱动 Verified）
- `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/SqliteCoreDomainDatabase.Objects.cs`（EF Core 原子发布事务）

### CI

- `.github/workflows/dotnet-ci.yml`（stacked PR Base 与功能分支 push 触发四平台矩阵）

## 4. Migration

- 没有新增或修改 Migration。
- 没有新增数据库字段；`AssetVerificationEvidence` 为事务内校验证据，不落库。
- 现有 FK / UNIQUE / CHECK 与 Snapshot 未变化；Model Snapshot 一致性测试通过。
- 现有 `core_object_definition_asset` FK 的删除行为不会造成孤立 link；单条删除路径仍受 FK 与 Domain 规则约束。
- 新 Migration 可从空数据库完整初始化（本次未新增）。

## 5. 新增测试及覆盖范围

### Domain

- `WorldView/tests/WorldView.Domain.Tests/Geometry/GeometryAndAssetRulesTests.cs`
  - 空 component/asset identity 拒绝；
  - role min / max；
  - `combined_min`；
  - 重复 Component ID；
  - 合法集合；
  - 未声明 role。
- `WorldView/tests/WorldView.Domain.Tests/Registry/JsonSchemaSubsetTests.cs`
  - `if` 命中 / 不命中；
  - `then` / `else`；
  - `not` 命中与不命中；
  - 条件子 schema 未支持关键字 fail-closed；
  - 真实 Building 条件规则与真实 Building/Cave typed schema 正反例。

### Infrastructure（真实 SQLite）

- `WorldView/tests/WorldView.Infrastructure.Tests/Assets/AssetIntegrityVerificationTests.cs`
  - 真实字节验证成功；
  - 文件缺失；
  - Length 不一致；
  - Hash 不一致；
  - 验证后文件被篡改 → 过期验证失效；
  - 读取失败（文件锁）；
  - 未验证 Asset 不可正式引用；
  - 已验证 Asset 可引用；
  - 实测摘要与元数据不匹配被拒绝且状态不变（Task 002-A 重构，替代旧伪造 evidence 用例）；
  - Verified 写入 API 面只接受实测摘要（Task 002-A 新增）；
  - 失败接口不能写 `Verified`。
- `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/GeometryAtomicPublicationTests.cs`
  - Definition 与完整 link set 一起成功；
  - 未验证 Asset 整体失败；
  - hash 不匹配整体失败；
  - 缺失 Asset 整体失败；
  - 既有 link set 多出无关 Asset 整体失败；
  - Definition 非 active 整体失败；
  - 发布中途触发器异常 → 整体回滚；
  - 单条 Link/Unlink/EditDefinition 无法绕过原子发布。
- 更新 `Cd02AssetPersistenceTests`、`Cd02MapEditorIntegrationTests` 以适配 Verified-only 与原子发布语义（Task 002-A 再适配实测摘要：Verified 测试资产一律登记真实字节 hash 并以 `MeasureAsync` 摘要写入）。

## 6. Windows 本地结果

Task 002 原始结果（历史记录）：

| 步骤 | 结果 |
|---|---|
| `dotnet restore WorldView/WorldView.sln` | 通过 |
| Debug Build | 通过（0 error） |
| Debug Test | 通过 527 / 失败 0 |
| Release Build | 通过 |
| Release Test | 通过 527 / 失败 0 |

Task 002-A 结果见 §0 A5（537 / 0）。

## 7. GitHub Actions 四平台结果（Task 002 实现）

Workflow `.NET CI`，run 577（[run 45132975988](https://github.com/luqizheng/worldview/actions/runs/45132975988)），`head_sha = d017449ab78f041eda2a612eb3f5c67bf9d61b92`。

| OS | Configuration | 结果 |
|---|---|---|
| Windows | Debug | success |
| Windows | Release | success |
| macOS | Debug | success |
| macOS | Release | success |

说明：PR #22 的 Base 是 PR #11 的功能分支，GitHub 默认 `pull_request` 过滤器只监听 `master`；因此本分支通过同时启用“功能分支 push”触发四平台矩阵，运行结果对应上述实现 SHA，未引用任何旧 SHA 记录。

Task 002-A 四平台 CI 见 §11。

## 8. 已知问题

- `VerifyAssetIntegrityUseCase` 的 `AssetNotFound`（Core Asset 行不存在）不写完整性状态，因为没有可更新的记录。
- `ReadFailed` 在状态模型中没有独立枚举，按 `Corrupt` 记录，但返回结果保留 `ReadFailed` 区分。
- 既有 link set 与既有 payload 不一致时，原子发布按旧 PR 语义 fail-closed，不自动修复；需要先排查/修复历史不一致。
- 当前仍保留 `CoreDomainAggregateModel.cs` 等与 PR #11 并存的旧模型定义文件；本任务未扩展它们，也未删除，避免超出范围。

## 9. 尚未解决的产品决策

- 生产 Elevation precision、quantization、Tile dimensions、codec profiles 未定义；本任务没有引入任何默认值。
- 验证摘要是否需要长期持久化审计（而非仅事务内校验）尚未确认。
- Building/Cave 之外 typed aggregate 是否允许存在独立 typed geometry 发布流程尚未确认。

## 10. 后续建议

- 在后续任务中统一清理 PR #11 之外的历史模型定义文件，避免维护双份映射。
- 若需要审计追溯，可单独设计 asset verification 审计持久化表（记录每次实测摘要与结果），而不是把凭证塞进现有资产表。
- 为 `ReadFailed` 增加独立持久化状态需先确认状态机语义，再变更 Migration。

## 11. Task 002-A 四平台 CI 结果

（推送后由实际 CI run 回填：run ID 与准确 HEAD SHA。）
