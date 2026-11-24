# WP-30A 上下文与指纹规范

## 1. PresentationContext 字段语义

| 字段 | 类型 | 来源/语义 | 指纹参与 |
|---|---|---|---|
| `MapId` | `Guid`（非空） | 经 Core Domain 校验的 Map 身份 | ✓ |
| `BranchId` | `Guid?`（null = 根历史/默认分支） | 解析结果；null 与空 GUID 经存在标志字节严格区分 | ✓ |
| `RootHistoryId` | `Guid`（非空） | 根历史 ID；WP-30A 起 Application 边界必须保留 | ✓ |
| `HistoricalTime` | `WorldView.Domain.Time.HistoricalTime` | typed 规范历史时间（DayNumber:long + OffsetSeconds:int ≤ 86399）；**不经字符串降级**，直接采用 Domain 不可变类型（全精度） | ✓ |
| `WorldStateRevision` | typed 语义态 | M1 恒为 `NotEvaluated` 单例 | ✓ |
| `Projection` | typed 语义态 | M1 恒为 `NotConfigured` 单例 | ✓ |
| `StyleToken` | `string`（非空白） | `BuiltinNeutralStyle/v1` | ✓ |
| `FontToken` | `string`（非空白） | `BuiltinFontBaseline/v1` | ✓ |
| `ViewportGeneration` | `long`（≥ 1） | 视口适配器拥有的单调代；请求创建时捕获 | ✓ |
| `SceneAssemblerVersion` | `int`（≥ 1） | 装配语义版本（M1 = `PresentationAssemblerVersions.BlankScene` = 1） | ✓ |
| `Fingerprint`（派生） | `ContextFingerprint` | 构造时由 canonical 序列化 + SHA-256 计算 | — |
| `SemanticIdentity`（派生） | `WorkspaceSemanticIdentity` | MapId+BranchId+RootHistoryId+HistoricalTime 四元组（语义切换判定依据） | — |

校验（fail-closed）：空 GUID、null typed 值、空白 token、ViewportGeneration/SceneAssemblerVersion
非正数一律 `ArgumentException`/`ArgumentOutOfRangeException`/`ArgumentNullException` 拒绝。

相等性：按全部身份字段的规范值比较（PRA-01 §2.5），字段相等 ⇔ 指纹相等（测试锁定）。

## 2. typed 语义态（防伪造边界）

### 2.1 WorldStateRevision（Profile §3.2）

- `NotEvaluated`：显式单例语义态——不是 null、空串、整数零或伪 revision。M1 唯一合法值。
  MapId/BranchId/RootHistoryId/typed 时间仍独立完整存在于上下文中（NotEvaluated 不抹除历史上下文）。
- `Evaluated(string RevisionIdentity)`：预留；身份非空白。canonical 序列化以类别字节 + 负载
  差异与 NotEvaluated 可区分；**任意** Evaluated 值与 NotEvaluated 指纹必不相同。
  未来真实 revision 只能来自权威 Core Domain/Application 读模型——Presentation 不成为其权威。
- 防伪造保证：M1 factory 固定注入 NotEvaluated；仓库中不存在任何把时间戳/ID/哈希当作
  revision 的代码路径（类型系统层面 Evaluated 只能显式构造）。

### 2.2 ProjectionDefinition（Profile §3.3）

- `NotConfigured`：显式单例；不猜测 EPSG/CRS/地理坐标系。
- `Configured(string DefinitionId, string DefinitionVersion)`：预留（双字段均非空白）。
- M1 的 Pan/Zoom 是屏幕导航变换，不属于本状态。

### 2.3 Style/Font token（Profile §3.4）

`BuiltinNeutralStyle/v1`、`BuiltinFontBaseline/v1`：显式命名 + schema 版本的呈现基线身份，
非 Domain revision、不暗示正式样式/字体子系统存在。影响输出的行为变化时递增版本。

## 3. Canonical 序列化（`PresentationContextCanonicalizer`，internal）

序列化 schema 版本：`1`（`PresentationContextCanonicalizer.SchemaVersion`，编码规则变化时递增）。

### 3.1 字节布局（字段顺序固定）

```
[int32 LE]    序列化 schema 版本（= 1）
[16 字节]     MapId（Guid.ToByteArray() 的固定 16 字节顺序）
[1 字节]      BranchId 存在标志：0x00 缺省（根历史）/ 0x01 显式
[16 字节]     BranchId（仅当标志 = 0x01）
[16 字节]     RootHistoryId
[int64 LE]    HistoricalTime.DayNumber
[int32 LE]    HistoricalTime.OffsetSeconds
[1 字节]      WorldStateRevision 类别：0x01 NotEvaluated / 0x02 Evaluated
[string]      Evaluated.RevisionIdentity（仅当 0x02）
[1 字节]      Projection 类别：0x01 NotConfigured / 0x02 Configured
[string]      Configured.DefinitionId（仅当 0x02）
[string]      Configured.DefinitionVersion（仅当 0x02）
[string]      StyleToken
[string]      FontToken
[int64 LE]    ViewportGeneration
[int32 LE]    SceneAssemblerVersion
```

### 3.2 编码规则

- **整数**：固定小端、定长（手写编码，显式可证，不依赖平台字节序假设）。
- **Guid**：`Guid.ToByteArray()` 固定 16 字节（值跨平台稳定）。
- **string**：`[int32 LE 字节长度] + UTF-8 原始字节`。不区分大小写折叠、不做文化相关
  规范化；身份比较按精确字节序列。
- **可区分性**：BranchId 的 null（标志 0x00）与 `Guid.Empty`（标志 0x01 + 全零）严格区分；
  NotEvaluated/Evaluated、NotConfigured/Configured 凭类别字节区分；所有必填串非空白，
  无 null/空串歧义。
- **排除项**：进程 ID、线程 ID、时间戳、随机数、对象地址、可变引用一律不参与。

## 4. ContextFingerprint

- 对 canonical 字节计算 **SHA-256**（`System.Security.Cryptography.SHA256.HashData`）。
- 值语义：32 字节私有副本；按字节比较；`ToHex()` = 64 字符小写十六进制（不变文化）。
- **Pinned 测试向量**（固定上下文：MapId `00010203-0405-0607-0809-0A0B0C0D0E0F`、
  BranchId `11121314-1516-1718-191A-1B1C1D1E1F20`、RootHistoryId
  `21222324-2526-2728-292A-2B2C2D2E2F30`、HistoricalTime `1200:43200`、NotEvaluated、
  NotConfigured、gen = 7、assembler = 1）：

  ```
  8d59a5649082ee21e2eb04ae0190d45c2ca9379dcefb884589befbb8356ac7e9
  ```

  该向量锁定全部编码规则；任何序列化变化都会使其失败（`PinnedSha256Vector_...` 测试）。

### 4.1 指纹行为保证（全部有测试）

1. 相同语义上下文（跨构建点/进程内重复）→ 相同指纹；
2. 每个身份字段独立变化 → 指纹必变（MapId/BranchId(null↔值)/RootHistoryId/时间
   day/offset 秒级精度/StyleToken/FontToken/ViewportGeneration/SceneAssemblerVersion）；
3. `NotEvaluated` vs `Evaluated("...")` 可区分；不同 Evaluated 之间可区分；
4. `NotConfigured` vs `Configured(...)` 可区分；
5. canonical 字节逐字节可复现；culture（tr-TR/ar-SA）与时区变化不改变指纹与
   `HistoricalTime.Canonical()` 输出；
6. 发布器以"期望指纹"校验候选：指纹不一致的候选不可发布。

## 5. 语义身份（`WorkspaceSemanticIdentity`）

- 四元组：`(MapId, BranchId, RootHistoryId, HistoricalTime)`，typed 值相等。
- 用途：发布器在每次请求激活时比较上一次激活的语义身份——发生变化即判定
  **语义上下文切换**，原子清空当前快照（旧上下文场景不得在新语义下冒充有效结果，
  M1 Profile §6.4）；仅视口代/样式 token 变化属同语义刷新，保留 last-known-good。
- 明确不参与语义身份：ViewportGeneration、StyleToken、FontToken、SceneAssemblerVersion
  （它们改变指纹 → 触发重新构建，但不改变"显示的是哪个工作区"这一语义）。
