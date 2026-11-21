# CD-02-D — Geometry & Asset Foundation Implementation Report V0.1

- **Date:** 2026-11-22
- **Baseline:** master `cb85f67`（CD-02-C 交付后）
- **Companion:** [CD-02-C Report](CD-02-C_IMPLEMENTATION_REPORT_v0.1.md)、[CD-02-A §12（Geometry/Asset ownership）](CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md)
- **结论：PASS WITH CONDITIONS**（编码决策状态见 §5；macOS 未验证）

---

## 1. 前置核对结论（委托 §5 要求）

| 契约 | 核对结果 |
|---|---|
| P2-09-19 V0.2 | V1 **不建** `core_geometry_component` 表（§3.2）；组件 manifest（10 规范字段）存 owner typed payload；typed link 仅 `core_object_definition_asset`（definition geometry，条件性）/ `core_map_initial_object_asset`（不启用）/ `core_command_asset`（Command 阶段）；split/merge deferred（§7.5）；二进制格式选择为 open gate（§12.1） |
| P2-09-25 V0.1 | **Recommended 非冻结 ADR**：canonical JSON envelope（`worldview.geometry-json`）为 V1 推荐方向；本阶段按此实现有界结构校验器并显式标注替换点 |
| ME-05-02 V0.1 | 所有权/身份规则与 P2-09-19 一致；候选几何不作正式 Asset；正式写 context 四分确认 |
| P2-09-41 V0.4 | Building/Cave descriptor（proposal）声明 geometry role 带 dimension + `asset_contract`/`child_aggregate_contract` 块——CD-02-E 输入 |
| P2-09-06 V0.1 | AI staging 永不进入 core_asset；发布后字节不可变；hash 验证先于正式引用 |
| P2-09-11 §4.6 | `core_asset` 列定义与 typed link 定义（owner keys + asset_id + role；UNIQUE 四元组） |

## 2. 交付内容

### 2.1 数据库（迁移 009–010）

| 迁移 | 表 | 关键约束 |
|---|---|---|
| `009_core_asset` | `core_asset` | PK asset_id；UNIQUE(project,hash_algorithm,content_hash)（内容寻址）；hash_algorithm CHECK `='sha-256'`；publication_state CHECK `=('published')`（staging 永不入库）；integrity_state CHECK (verified/unverified/corrupt/missing) |
| `010_core_object_definition_asset` | `core_object_definition_asset` | UNIQUE 四元组 (project,identity,asset,role)；复合 FK→core_object、→core_asset；行仅含 owner keys + asset_id + **role**（绝不携带 hash/组件 ID，CD-02-A §12） |

### 2.2 Domain（`WorldView.Domain/Assets/`、`WorldView.Domain/Geometry/`）

- **Asset 权威元数据**：`AssetRecord` + `AssetRules`（SHA-256 规范形式校验、内容寻址相对路径派生与圈禁、`Sha256Hex`/`ValidateHashMatches` fail-closed）+ `AssetService`（Register 内容寻址幂等；Link/Unlink definition asset link；corrupt/missing Asset 拒绝新引用；draft Object 拒绝 link；role 必须为 registry 已声明 geometry role）。
- **Geometry Component 领域表示**：`GeometryComponentManifest`（P2-09-19 §6.2 十规范字段）+ `GeometryRules.ValidateManifest`（role 已声明、family ∈ role families、dimension=2、schema 版本>0、hash 规范）+ `ValidateManifestConsistency`（manifest ↔ typed link ↔ core_asset 三方一致；权威 hash 从 core_asset 解析比对）。
- **Geometry Asset envelope 校验**（`GeometryAssetEnvelope`）：P2-09-25 推荐包络的有界结构实现——format_id/format_version/spatial_reference_id/axis_order/unit/precision profile 存在性与类型、body 与 family 形状一致（point/line_string/polygon/multi_*/curve）、坐标有限数值、环闭合、未知曲线段类型拒绝（不静默线性化）。**显式标注**：P2-09-25 为 Recommended 方向，若编码决策变更本类是唯一替换点。
- **Geometry 操作策略**：`GeometryOperationRules`——InPlaceAdjust/Refinement 保留组件 ID、AddComponent 新 ID、RemoveComponent 结束 membership；`RejectSplitMerge` 类型化拒绝（split/merge V1 不支持，绝不经 add/remove 链式模拟，GEO-07/08）。Command 写路径本身属 CD-03+，本阶段仅落地分类与拒绝边界。

### 2.3 端口/适配器/测试替身

- `ICoreDomainDatabase` 增 10 个端口方法（Asset×6 + definition link×4）；适配器 `SqliteCoreDomainDatabase.Assets.cs`（单事务临界区模式）；`FakeCoreDomainDatabase` 同步。

## 3. 测试与真实结果

**全量 322/322 通过（Debug，真实执行）；Release 构建 0 错误。** 新增 41 个测试：

- Domain（33）：hash 规范/路径圈禁/字节 hash 篡改 fail-closed；manifest role/family/dimension/hash 负向 + 合法组合（river.centerline、settlement.anchor）；三方一致性（hash/role 不匹配拒绝）；操作策略分类 + split/merge 拒绝；envelope 正反向（point/line/polygon 闭合/开环/multi_polygon/curve 线段+贝塞尔/未知段类型/family 不匹配/dimension 不匹配/format_id 错误/NaN 字面量拒绝）。
- Infrastructure（8，真实 SQLite）：登记元数据 + 派生路径；同内容幂等；重复 hash UNIQUE 负向；完整性状态转换（corrupt 阻止新引用）；FK 链 + role 唯一 + 未声明 role 拒绝；draft Object 拒绝 link；孤儿对账（未引用 Asset 识别）；**全链集成**：发布字节 → hash 登记 → link → envelope 校验 → manifest 三方一致性。

## 4. 边界（明确不做）

- `core_map_initial_object_asset` 不建（Initial State payload 无独立 geometry Asset 引用；触发条件见 CD-02-A §17.3）。
- `core_command_asset` 不建（Command 阶段必需，CD-03+）。
- 无 `core_geometry_component` 表（V1 明确不建）；组件 manifest 进入 owner payload 的载体在 Building/Cave definition schema（CD-02-E）。
- 精度量化策略/spatial reference 定义/环朝向深校验：待 P2-09-25 §4/§5 的 spatial-reference 契约关门。
- 几何 Command 写路径（core.geometry.*）：CD-03+ Command 阶段。

## 5. 条件与遗留

1. geometry payload 编码按 P2-09-25 **Recommended 方向**实现结构校验——该决策非冻结 ADR；变更时替换点已隔离。
2. 字节级完整性验证组合点：元数据层（本阶段）+ 字节层（FileAssetStore 已有篡改拒绝）；组合用例（IAssetStore + core_asset 对账）随 Application 用例阶段补充。
3. macOS 未验证；P2-09-12 完整证明套件未执行（延续既有条件）。

## 6. Git

本阶段单 commit（迁移 + Domain + 端口/适配器 + 测试 + 文档）。
