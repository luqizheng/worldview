# Task 002 — EF Core V1 Asset / Geometry Invariant Port

- 分支：`feat/efcore-v1-asset-geometry-invariants`
- Base：`feat/efcore-v1-complete-model`（PR #11 Head，Draft 保持）
- Stacked PR：[#22](https://github.com/luqizheng/worldview/pull/22)（Draft，Base 指向 PR #11 Head）
- 基线 SHA：`991aa17b7a3f6f2588aabf596e2e808b6c9cdde2`
- 实现提交 SHA：`d017449ab78f041eda2a612eb3f5c67bf9d61b92`
- 状态：本地 Windows Debug/Release 全部通过；四平台 GitHub Actions 全部成功

## 1. 实际迁移的领域规则

### G1 — Asset Integrity

- 正式 Geometry Definition 仅可引用 `Verified` Asset；`Unverified` 与 `Corrupt/Missing` 均拒绝建立正式 typed link。
- 唯一可写入 `Verified` 的路径为 `MarkAssetVerified(evidence)`，且持久化适配器在写事务内重读当前 Asset 元数据，要求 evidence 的 expected/actual hash 与 byte length 一致并与数据库值匹配。
- `RecordAssetIntegrityFailure` 只接受 `Corrupt/Missing`，从接口层杜绝调用方直接写 `Verified`。
- `PublishObjectDefinitionWithGeometryAssets` 在 SQLite 事务内重读 Object 生命周期、Definition CAS、Asset `content_hash` 与 `integrity_state`。
- `LinkDefinitionAsset` 的正式引用错误与 `Publish...` 的失败均以明确异常返回；发布事务失败整体回滚，不留部分 Definition 或 Link。

### G2 — Asset Byte Verification

- 新增 `AssetVerificationEvidence(ExpectedHash, ActualHash, ExpectedByteLength, ActualByteLength)`。
- 新增 Application 用例 `VerifyAssetIntegrityUseCase`：
  - 通过 `IAssetStore.OpenReadAsync` 读取真实不可变字节；
  - SHA-256 与长度均由实际字节流计算；
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
| #2 G2 Byte Verification | `feat/cd-02-g2-asset-byte-verification` | 已迁移 `AssetVerificationEvidence`、`MarkAssetVerified`、`RecordAssetIntegrityFailure` 与真实字节验证用例。 |
| #5 Manifest Identity | `fix/cd-02-g3-geometry-manifest-identities` | 已迁移空 ID 校验与 `ValidateManifestCollection`（min/max/combined_min/唯一性）。 |
| #6 Conditional Schema | `feat/json-schema-conditional-keywords` | 已迁移 `if/then/else/not` 与 fail-closed 扩展，并补充真实 Building/Cave 测试。 |
| #7 Atomic Publication | `feat/cd-02-g3-atomic-geometry-publication` | 语义已迁移并重新实现为 EF Core 单事务版本；未恢复旧 SQLite Adapter。 |

旧分支与当前领域模型未发现需要改变已确认语义的冲突。旧 PR 均未合并、未关闭、Base 未修改。

## 3. 新增 / 修改文件

### Domain

- `WorldView/src/WorldView.Domain/Assets/AssetRecords.cs`（新增 `AssetVerificationEvidence`）
- `WorldView/src/WorldView.Domain/Assets/AssetService.cs`（Verified-only 引用；Building/Cave 禁止单条 link/unlink）
- `WorldView/src/WorldView.Domain/Aggregates/BuildingCaveAggregateValidator.cs`（发布集合严格一致性）
- `WorldView/src/WorldView.Domain/Geometry/GeometryRules.cs`（空身份 + `ValidateManifestCollection`）
- `WorldView/src/WorldView.Domain/Objects/ObjectPlans.cs`（`ObjectDefinitionPublicationPlan`）
- `WorldView/src/WorldView.Domain/Objects/MapInitialStateService.cs`（原子发布服务；`EditDefinition` 组件变更保护）
- `WorldView/src/WorldView.Domain/Objects/ObjectCreationService.cs`（Building/Cave 初始组件必须为空）
- `WorldView/src/WorldView.Domain/Persistence/ICoreDomainDatabase.cs`（验证证据 API；原子发布 API）
- `WorldView/src/WorldView.Domain/Registry/JsonSchemaSubset.cs`（条件关键字）

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
  - 过期/不一致 evidence 被拒绝且状态不变；
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
- 更新 `Cd02AssetPersistenceTests`、`Cd02MapEditorIntegrationTests` 以适配 Verified-only 与原子发布语义。

## 6. Windows 本地结果

| 步骤 | 结果 |
|---|---|
| `dotnet restore WorldView/WorldView.sln` | 通过 |
| Debug Build | 通过（0 error） |
| Debug Test | 通过 527 / 失败 0 |
| Release Build | 通过 |
| Release Test | 通过 527 / 失败 0 |

## 7. GitHub Actions 四平台结果

Workflow `.NET CI`，run 577（[run 45132975988](https://github.com/luqizheng/worldview/actions/runs/45132975988)），`head_sha = d017449ab78f041eda2a612eb3f5c67bf9d61b92`。

| OS | Configuration | 结果 |
|---|---|---|
| Windows | Debug | success |
| Windows | Release | success |
| macOS | Debug | success |
| macOS | Release | success |

说明：PR #22 的 Base 是 PR #11 的功能分支，GitHub 默认 `pull_request` 过滤器只监听 `master`；因此本分支通过同时启用“功能分支 push”触发四平台矩阵，运行结果对应上述实现 SHA，未引用任何旧 SHA 记录。

## 8. 已知问题

- `VerifyAssetIntegrityUseCase` 的 `AssetNotFound`（Core Asset 行不存在）不写完整性状态，因为没有可更新的记录。
- `ReadFailed` 在状态模型中没有独立枚举，按 `Corrupt` 记录，但返回结果保留 `ReadFailed` 区分。
- 既有 link set 与既有 payload 不一致时，原子发布按旧 PR 语义 fail-closed，不自动修复；需要先排查/修复历史不一致。
- 当前仍保留 `CoreDomainAggregateModel.cs` 等与 PR #11 并存的旧模型定义文件；本任务未扩展它们，也未删除，避免超出范围。

## 9. 尚未解决的产品决策

- 生产 Elevation precision、quantization、Tile dimensions、codec profiles 未定义；本任务没有引入任何默认值。
- 验证证据是否需要长期持久化审计（而非仅事务内校验）尚未确认。
- Building/Cave 之外 typed aggregate 是否允许存在独立 typed geometry 发布流程尚未确认。

## 10. 后续建议

- 在后续任务中统一清理 PR #11 之外的历史模型定义文件，避免维护双份映射。
- 若需要审计追溯，可单独设计 `asset_verification_evidence` 持久化表，而不是把证据塞进现有资产表。
- 为 `ReadFailed` 增加独立持久化状态需先确认状态机语义，再变更 Migration。
