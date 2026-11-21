# CD-02-C — Registry Runtime & Typed Schema Implementation Report V0.1

- **Date:** 2026-11-22
- **Baseline:** master `648f9dd`（CD-02-B 交付后）→ 本阶段交付（见 §7 Git）
- **Companion:** [CD-02-B Implementation Report](CD-02-B_IMPLEMENTATION_REPORT_v0.1.md)、[CD-02-A §7（Registry 边界）](CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md)
- **结论：PASS WITH CONDITIONS**（V2 激活缺口清单见 §6；macOS 未验证）

---

## 1. R1 Schema artifacts 盘点结论

| Artifact | 状态 | 可用性 |
|---|---|---|
| P2-09-26 V0.2 descriptor registry（format_version=1，十类型） | **完整且机器可读** | 每类型 12 个顶层键全部齐备（type_key/schema_version/semantics_id/allocatable/geometry/definition/runtime/invariants/command_capabilities/definition_edit_policy/initial_state_policy/runtime_attribute_change_policy）；`common_rules.unknown_fields="reject"` 显式声明未知字段拒绝 |
| definition 字段声明（十类型） | **完整** | 字段词汇 6 种形式：`required string` / `optional string` / 有界枚举（字符串数组）/ `non_negative_decimal` / `required stable reference` / `optional immutable Asset reference`；十类型全部字段可归属到这六种（无未知声明） |
| geometry role 声明 | **完整** | 每类型 role → {min, max, families}；mountain_range 另有 `combined_min` 合计下限（非 role） |
| P2-09-24 字段目录（人工目录） | 与 registry **一致** | 逐类型核对无冲突（road/terrain_region 的 canonical_name 可选性两处来源一致） |
| runtime 字段 | 声明但全部 disabled_pending_* | 启用需对应契约（temporal datum/hydrology/provenance），不在本阶段 |
| initial_state typed 字段 schema | **不存在**（OD-1 维持 open） | initial_state_policy 只声明 includes 列表（map_membership/initial_placement/initial_runtime_attributes），无逐字段 schema——Initial State payload 保持结构层校验 |
| P2-09-28 validation vectors（53） | **纯文字设计夹具** | 无具体输入 payload；OT-00x 中"缺必填字段/枚举越界"类已编码为单元测试向量；几何/关系类分别依赖 CD-02-D 与 Relationship 阶段 |
| P2-09-46 V0.2 / P2-09-47 V0.2（Format V2 schema + 34 vectors） | 设计态 | V2 激活门未执行（§6）；对 V1 运行时不适用 |

**结论：definition/geometry/pin 的 typed 校验有可靠 schema artifacts（P2-09-26 V0.2），可启用；Initial State typed 校验无 artifacts（OD-1 不发明）。**

## 2. 交付内容

### 2.1 Registry 运行时（`WorldView.Domain/Registry/ObjectTypeRegistry.cs`）

- **数据源**：P2-09-26 V0.2 JSON 经 csproj `EmbeddedResource` 链接嵌入（单一数据源，无复制件漂移；编译期链接，无 docs 运行时依赖）。首次访问惰性加载 + 线程安全单例。
- **加载即 fail-closed 结构校验**：format/format_version=1；descriptor 未知键拒绝；type_key 与目录键一致；semantics_id 模板强校验（`world-view.object-type.<key>.v1`）；geometry 基数区间合法；definition 声明词汇白名单；枚举非空无重复；runtime 值必须为字符串状态。
- **运行时 API**（全部 fail-closed + 稳定错误码 `RegistryException.ErrorCode`）：
  - `All` / `Find` / `Require`（未知 type_key → `unknown_type_key`，OT-01）
  - `ValidatePin`（pin ≠ descriptor 声明 → `pin_mismatch`，OT-05/06）
  - `ValidateDefinitionPayload`（未知字段 → `unknown_definition_field`；缺必填/类型错/枚举越界/负数/空白引用 → `definition_invalid`）——**OD-2 的运行时启用**，校验依据 = descriptor 数据，零字段语义发明
  - `ValidateGeometryRole`（declared_geometry_roles_only 政策 → `unknown_geometry_role`）
  - `InitialStateIncludes`（结构级 includes 暴露；typed 校验维持 OD-1 open）
- **权威迁移**：pin 推导从 `BuiltinObjectTypes` 常量迁移到 registry（`BuiltinObjectTypes.SemanticsIdFor`/`DescriptorSchemaVersionFor` 变为委托；种子目录仍拥有显示名/type_key 列表用于播种）。`ObjectRules.ValidatePin` 包装 `RegistryException` 为 `CoreDomainConflictException`（保留错误码透传）。

### 2.2 服务接线

| 服务 | 接线点 |
|---|---|
| `ObjectIdReservationService.Bind` | pin 由 `Registry.Require(typeKey)` 推导（原 BuiltinObjectTypes 常量） |
| `ObjectCreationService.InsertObject` | definition payload typed 校验（canonical 化后） |
| `ObjectDefinitionService.EditDefinition` | 新 payload typed 校验（canonical 化后，CAS 之前） |
| 适配器 `BindReservation` | core_object 行写入 descriptor 的 SchemaVersion/SemanticsId（registry 值） |
| Initial State | **不接线 typed 校验**（OD-1 无 artifacts；保持结构层 + canonical fingerprint） |

### 2.3 新增异常

`RegistryException`（稳定错误码）；`CoreDomainConflictException` 增加内异常构造函数。

## 3. 测试与真实结果

**全量 281/281 通过（Debug，真实执行）；Release 构建 0 错误。** 新增 13 个 Registry 测试（`ObjectTypeRegistryTests`）：

- 加载结构校验：10 descriptor、键唯一、semantics_id 模板、allocatable、definition/geometry 非空、`map_membership` 在 includes、Building/Cave 不在目录。
- pin：十类型全通过；版本/语义不符 → `pin_mismatch`；未知键 → `unknown_type_key`。
- **十类型程序化正向向量**：按各 descriptor 声明自动构造最小合法 definition payload，逐类型通过（声明可满足性证明，不发明值）。
- 负向向量：缺必填、枚举越界、未知字段、字符串字段给数值、负数高程（mountain_peak）、缺 vertical_reference_id、可选字段（terrain_region/road）正反、非 object 根拒绝。
- geometry role：声明 role 通过；未声明 role/`combined_min` 冒充 role 拒绝；mountain_range 合计下限解析正确。
- 种子目录 ↔ registry 一致性（十 key + pin 值）。

既有测试同步更新：`FakeCoreDomainDatabase.Bind`、Domain/Infrastructure 测试 payload 全部改为各类型合法 definition（river/road/mountain 定义）。

## 4. 兼容性验证（十类型）

1. 加载：十类型 descriptor 全部通过结构校验 ✅
2. pin：十类型 pin 值与 CD-02-B 播种/绑定写入值一致（种子目录一致性测试）✅
3. typed payload：十类型正向向量 + 逐类型负向（river/road/terrain_region/mountain_peak 显式覆盖）✅
4. 既有 268 测试零回归（payload 更新后 281 全绿）✅

## 5. 边界与不做

- Registry V2 **未激活**（见 §6）；本运行时是 V1 目录（format_version=1）读取层。
- 无 registry compiler（跨记录引用矩阵/asset 三元组校验等十二项职责属 V2 激活门，未实现）。
- Command capabilities 仅原样保留（JsonElement），消费方为 Command 阶段。
- Runtime 字段（disabled_pending_*）不启用。
- Initial State payload typed 校验未启用（OD-1）。

## 6. Registry V2 激活缺口清单（R5，如实报告）

| # | P2-09-45 §8 验收门 | 现状 |
|---|---|---|
| 1 | P2-09-46 V0.2 schema 过真实 Draft 2020-12 meta-schema 验证 | 未执行（需 meta-schema validator） |
| 2 | P2-09-47 V0.2 34 vectors 生产级执行 | 未执行 |
| 3 | V2 compiler 十二项跨记录职责实现 | 未实现 |
| 4 | 编译产物（嵌入式 bundle）机制 | 未实现（当前直接嵌 descriptor 源文件） |
| 5 | legacy 十类型语义回归（V1→V2 迁移不改语义） | 部分基础（pin/typed 校验已建立），V2 未至 |
| 6–10 | 迁移策略/兼容证明/激活流程等 | 未开始 |

**建议**：V2 激活作为独立阶段（不阻塞 CD-02-D/E/F）；V1 运行时已满足当前所有写路径需求。

## 7. Git

本阶段单 commit（实现 + 测试 + 文档）：见提交列表 `feat(core-domain): CD-02-C registry runtime`。
