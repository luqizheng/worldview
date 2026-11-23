# 002 — Descriptor / Map Space / Spatial Reference Owner Context Validation

**日期**：2026-11-23
**分支**：`feat/field-package-reader-writer`（PR #17 head，stacked on PR #11）
**状态**：Windows 实施完成，538/538 通过；macOS 待 GitHub Actions

## 1. 任务目标

检查 Field Package 打开和导入时是否已验证 Descriptor / Map Space / Spatial Reference 的所属关系；补齐缺失的归属校验层，使包内引用的上下文必须与目标项目一致，禁止跨项目/跨地图错误引用，且错误信息能明确指出具体不匹配的引用。

### 检查结论（实施前的代码现状）

- `FieldPackageReader`（二进制层）：仅做格式级验证（header/directory/tile/mask/codec/hash），**不含任何领域归属校验**——符合"Reader 负责二进制结构"的原则，保持不动。
- `FieldPackageManifestValidator`：JCS 规范性 + Draft 2020-12 schema 校验，不含归属关系。
- `FieldDatasetService.PublishRevision`（发布时刻）：已含完整的 owner-context 校验（`SpatialReferenceRules.ValidateFieldOwnerContext`：Map 归属、Map Space、Spatial Reference、precision profile、vertical reference）。
- **缺口**：打开/导入时（发布之前、字节 staging 之前）没有独立的 owner-context 预检。调用方只能先走完发布流程才能发现归属不匹配，且发布期的 `CoreDomainConflictException` 无机器可读错误码，无法与"格式错误"区分分类。

## 2. 实际完成内容

新增 Application 层 owner-context 预检服务 `FieldPackageOwnerContextValidator`（`WorldView.Application.Fields` 命名空间），输入为已打开包的 canonical manifest 字节（即 `FieldPackageReader.ManifestUtf8`）与目标 `(projectId, mapId)`，校验链：

1. 目标 Project 存在（`FindProject`）→ 否则 `WVPK_OWNER_PROJECT_MISSING`。
2. 目标 Map 存在且属于该 Project（`FindMap` + 显式 `map.ProjectId` 比对）→ 否则 `WVPK_OWNER_MAP_MISSING`（跨项目/不存在同码，信息分别命名双方 ID）。
3. manifest 字节必须已是 RFC 8785 canonical UTF-8（`FieldManifestCanonicalizer.RequireCanonicalUtf8`，不静默修复）→ 否则 `WVPK_OWNER_MANIFEST_INVALID`。
4. Descriptor 归属：从 manifest 读取 `field.field_key`/`schema_version`/`semantics_id`，经 `FieldDescriptorRegistry` 验证已注册且 semantics 一致 → 否则 `WVPK_DESCRIPTOR_MISMATCH`（采用 ME-05-10 §10 既有码）。
5. 语义整体验证：调用发布权威 `FieldDatasetService.ValidateManifest`（与发布路径同一实现，杜绝预检与发布规则漂移）→ 失败 `WVPK_OWNER_MANIFEST_INVALID`（内层异常保留）。
6. Map Space 归属：`metadata.MapSpaceId` 必须等于 `map.MapSpaceId`；Map 行缺 Map Space 身份为 fail-closed → `WVPK_OWNER_MAP_SPACE_UNSET` / `WVPK_OWNER_MAP_SPACE_MISMATCH`。
7. Spatial Reference 归属：Map 必须已绑定（`WVPK_OWNER_SPATIAL_REFERENCE_UNBOUND`）；manifest `spatial_reference_id` 必须等于绑定值（`WVPK_OWNER_SPATIAL_REFERENCE_MISMATCH`）；定义行经 `SpatialReferenceService.FindDefinition`（读取时重验指纹）解析，缺失 fail-closed → `WVPK_OWNER_SPATIAL_REFERENCE_MISSING`。
8. Precision profile：manifest 快照 ID 必须等于绑定定义 pin 的 profile → `WVPK_OWNER_PRECISION_PROFILE_MISMATCH`。
9. Vertical reference：elevation 必须匹配定义的 `vertical_reference_id`，categorical 不得携带 → `WVPK_OWNER_VERTICAL_REFERENCE_MISMATCH`。
10. 兜底：调用发布权威 `SpatialReferenceRules.ValidateFieldOwnerContext`，预检结果与发布权威不一致视为校验器缺陷 → `WVPK_OWNER_CONTEXT_CONFLICT`。

校验通过返回 `FieldPackageOwnerContextValidation { Metadata, Map, SpatialReference }`（typed 投影 + 已解析的归属行），后续导入适配层可直接使用而无需二次查询。

全部失败均以 `FieldPackageValidationException` 抛出（Code 机器可读），Message 同时给出包内值与目标值。

## 3. 修改文件清单

| 文件 | 变更 |
|---|---|
| `WorldView/src/WorldView.Application/Fields/FieldPackageOwnerContextValidator.cs` | 新增：owner-context 预检服务、错误码表、结果记录 |
| `WorldView/src/WorldView.Application/Packaging/FieldPackageManifestValidator.cs` | 新增常量 `FieldPackageErrorCodes.DescriptorMismatch = "WVPK_DESCRIPTOR_MISMATCH"`（ME-05-10 §10 既有码，纯增量） |
| `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/FieldPackageOwnerContextValidationTests.cs` | 新增：16 个真实 SQLite 测试 |
| `WorldView/tests/WorldView.Infrastructure.Tests/WorldView.Infrastructure.Tests.csproj` | 嵌入 golden manifest `minimal-elevation-2x2.manifest.json` 资源 |
| `docs/development/local-progress/README.md` | 新增：进度目录索引 |
| `docs/development/local-progress/field-package/002-owner-context-validation.md` | 本报告 |

## 4. 重要实现决策

1. **分层边界**（ME-05-11 §3/§9）：二进制 Reader 保持格式级验证，不查数据库；归属校验作为独立 Application 层服务经 `ICoreDomainDatabase` 端口解析权威行。未在 Reader 中引入 ME-05-10 §3.1 的 `expectedFieldContext` 参数（该签名允许 caller 直传期望上下文）；本实现由校验器自行从数据库解析目标上下文，调用方无法传入错误的"期望值"。
2. **单一权威**：语义 manifest 校验与 owner 兜底校验直接复用发布路径的 `FieldDatasetService.ValidateManifest` / `SpatialReferenceRules.ValidateFieldOwnerContext`，预检通过 ⇒ 发布必然通过同一 owner 校验（测试兜底码 `WVPK_OWNER_CONTEXT_CONFLICT` 保障漂移可检测）。
3. **fail-closed 输入**：manifest 字节要求 canonical，不做规范化修复；与 Reader 契约一致（导入输入即 `ManifestUtf8`）。
4. **错误码族**：新增 `WVPK_OWNER_*`（目标态缺失/不一致）+ 复用 `WVPK_DESCRIPTOR_MISMATCH`（描述符归属，ME-05-10 已定义语义）；目标态失败（Project/Map 缺失、未绑定）与包缺陷（引用不匹配）可按码区分。这是对 ME-05-10 taxonomy 的**增量扩展**（该表属于 reader/writer 组件，owner-context 校验器属于 publication coordinator 边界），未改动任何既有码。
5. **无新增并发机制**：校验为顺序只读查询，无锁、无缓存失效语义。

## 5. Windows 编译结果

- `dotnet build -c Debug`：**成功，0 错误**（含 325 条既有警告，均为本任务之前已存在的 xUnit1051/AVLN5001 等，非本次引入）。
- `dotnet build -c Release`：**成功，0 错误**。

## 6. Windows 单元测试结果

- 新增测试类 16 个用例全部通过（`Total: 16, Failed: 0`）。
- 全解决方案 Debug：**538/538 通过**。
- 全解决方案 Release：**538/538 通过**。

## 7. 执行的测试命令

```text
dotnet build -c Debug --nologo
dotnet test WorldView.sln -c Debug --nologo        # 538/538
dotnet build -c Release --nologo -v q
dotnet test WorldView.sln -c Release --nologo      # 538/538
# 单类过滤（MTP/xunit.v3 直接调用程序集）：
dotnet tests/WorldView.Infrastructure.Tests/bin/Debug/net11.0/WorldView.Infrastructure.Tests.dll \
  -class "WorldView.Infrastructure.Tests.Persistence.CoreDomain.FieldPackageOwnerContextValidationTests"
```

## 8. 未解决问题

1. `WVPK_OWNER_SPATIAL_REFERENCE_MISSING`（绑定行存在但定义行缺失）无专门测试：正常端口路径无法构造该状态（`BindInitial` 要求定义先注册；一库一项目），属防御性 fail-closed 分支，由代码评审覆盖。
2. "跨项目 Map 引用"在 V1 数据库中结构性不可达（`InsertProject` 一库一项目不变量 + `FindMap` 按 Project 范围隔离）；校验器内保留显式 `map.ProjectId` 比对作为自文档化守卫，拒绝路径与 unknown-map 相同（`WVPK_OWNER_MAP_MISSING`）。跨项目拒绝的端口级行为由既有 `FakeCoreDomainDatabase` 契约（`FindMap` 跨项目返回 null）保障。
3. `value_encoding.profile_scope = "golden_fixture_only"` 的包当前可通过 owner-context 预检（与发布权威一致）；"fixture-only profile 不得作为生产导入"的产品级闸门属于 ME-05-11 §7 既有 deferred gate，未在本任务擅自收紧。

## 9. 需要 GitHub Actions 验证的内容

- macOS Debug/Release 构建与全部 538 个测试（含本任务 16 个新增真实 SQLite 测试）。
- 错误码与校验结果跨平台稳定（无平台分支逻辑，预期一致）。

## 10. Git commit SHA

commit 7193f39（feat(fields): add field package owner-context import preflight），已推送 origin/feat/field-package-reader-writer。

## 11. 与原设计不一致的地方

1. 任务描述要求验证"Field Package 中引用的上下文是否与目标项目一致"，其中 elevation 的 `vertical_reference_id` 与 categorical 不得携带 vertical 的规则实际上由语义 manifest 权威（`ValidateManifest` 的属性集合检查）先行强制：categorical 包携带 vertical 在 owner-context 比对之前即以 `WVPK_OWNER_MANIFEST_INVALID` 拒绝（测试固化该行为）；`WVPK_OWNER_VERTICAL_REFERENCE_MISMATCH` 仅在 elevation 的 ID 不一致路径触发。这与发布权威行为完全一致，非偏离，仅记录错误码归属的差异。
2. ME-05-10 §3.1 的 `OpenAsync(source, expectedFieldContext, ...)` 签名未采用（见决策 1）；reader 的打开签名保持现状 `Open(source, options, leaveOpen)`。
