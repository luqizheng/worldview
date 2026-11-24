# WP-30A 架构审查（Architecture Review）

审查基线：PR #27 @ `db31e72`。所有行号以代码提交 `9e6d777` 的文件内容为准。

## 1. 架构边界（任务 §四.A）

### 1.1 项目引用与 NuGet 依赖（实际工程文件核对，非仅命名空间）

| 工程 | 引用 | 结论 |
|---|---|---|
| `WorldView.Presentation.csproj` | 仅 `ProjectReference → WorldView.Application`；**零** PackageReference；`InternalsVisibleTo` 仅测试程序集 | ✓ |
| `WorldView.Presentation.Tests.csproj` | 仅引用 Presentation + xunit.v3/MTP 测试栈（与仓库既有测试工程逐字节同构的包引用集合） | ✓ |
| `WorldView.Application.csproj` | → Domain；包：Newtonsoft.Json、Corvus.Text.Json | 传递暴露给 Presentation 但 Presentation 源码零使用（grep 核实） |
| `WorldView.Desktop` | **本阶段零改动**（`c05ca48..db31e72` diff 核实） | 组合根未提前引入渲染依赖 ✓ |

- Presentation 对 Infrastructure、数据库（Sqlite/EF）、Avalonia、SkiaSharp、ViewModel、UI Dispatcher：**无直接引用、无传递编译依赖、无源码使用**。`PresentationBoundaryTests` 以程序集引用白名单 + 公开 API 反射双轨锁定（禁名单含 Avalonia/Skia/Sqlite/EntityFramework/Dapper/Npgsql/Microsoft.Data/WorldView.Infrastructure/WorldView.Desktop/CommunityToolkit）。
- 程序集实际引用集（边界测试断言）：`WorldView.Application` + `WorldView.Domain`（HistoricalTime typed 值经 Application 传递依赖暴露——依赖方向 Presentation → Application → Domain，单向合法，符合 Profile §4.1 "不得为恢复字段而添加 Presentation→Domain 直接依赖"——Presentation 源文件 `using WorldView.Domain.Time` 属该传递暴露）。
- 静态全局状态：grep 全源码，仅不可变单例（`NotEvaluated.Instance`、`NotConfigured.Instance`）与 static 方法/常量；无任何可变静态字段。服务生命周期：无 DI 注册、无单例服务；每视口一 runtime（`ViewportPresentationRuntime.Create()` 私有三件套），视口间零共享（实例字段持有，测试 `IndependentViewports_DoNotSupersedeOrOverwriteEachOther` 锁定）。
- **绕过 Application 获取 Domain 数据的途径：不存在。** 唯一数据入口是 `PresentationContextFactory.FromValidatedWorkspace(ValidatedWorkspaceContext, …)`；Presentation 不含任何持久化/查询代码路径（`ScenePreparation` 委托由协调器持有，M1 默认实现为纯内存空白场景组合，无 IO）。

### 1.2 Application → Presentation 真实数据路径（B 项重点）

```
Domain WorkspaceContextRules.TryResolve（原子快照作用域内）
  → ResolvedWorkspaceContext（ProjectId/MapId/MapName/RootHistoryId/BranchId/Lineage/Time: HistoricalTime）
  → CoreDomainWorkspaceContextResolver 映射（CoreDomainAdapters.cs:255-265，只映射不改规则）
  → ValidatedWorkspaceContext（MapId/BranchId/RootHistoryId required/HistoricalTime required；CanonicalHistoricalTime=计算属性）
  → ProjectSession.OpenWorkspaceAsync → MapWorkspaceSession.Context（Application/Sessions/MapWorkspaceSession.cs:24,35）
  → PresentationContextFactory.FromValidatedWorkspace（映射不丢字段、fail-closed）
  → PresentationContext（构造期计算 Fingerprint + SemanticIdentity，全部字段 readonly）
```

- **无字符串化重建**：typed `HistoricalTime` 对象全程同一实例（工厂测试 `Assert.Same` 证明）；`CanonicalHistoricalTime` 是计算属性，不是第二事实来源（会话测试锁定前导零输入规范化行为）。
- **PresentationContext 不保留活会话/可变实体**：全部字段为值或不可变 Domain 类型（`HistoricalTime` 为不可变 sealed class，构造后只读——符合 PRA-01 §1.2 "mutable Core Domain entities" 禁令的字面边界；交付文档 §4.1 对此有明确论证）。
- `MapWorkspaceSession.SwitchContextAsync` 在 `_gate` 内替换 Context + 取消派生工作——WP-30B 接线后，语义切换会经新请求激活路径触发快照清空（见并发审查 §4）。

## 2. PresentationContext 完整性（任务 §四.B）

| Profile §3.1 要求字段 | 实现 | 证据 |
|---|---|---|
| MapId | `Guid` 非空校验 | PresentationContext.cs:65；指纹参与 |
| BranchId | `Guid?`（null=根历史）；null 与 Guid.Empty 经存在标志字节严格区分 | :66；Canonicalizer :58-66；测试 `CanonicalSerialization_DistinguishesAbsentBranchFromGuidZero` |
| RootHistoryId | `Guid` 非空；工厂对空 GUID fail-closed | :67-69；Factory :32-35；e2e 断言 |
| HistoricalTime | Domain typed 全精度（DayNumber:long + OffsetSeconds:int ≤ 86399） | :70；指纹按 int64/int32 LE 编码保留秒级精度（测试 1 秒差异必变） |
| WorldStateRevision | `NotEvaluated` 单例语义态 | 见 DoD-3 |
| Projection | `NotConfigured` 单例语义态 | 见 DoD-3 |
| Style/Font | 显式 token 常量 `BuiltinNeutralStyle/v1` / `BuiltinFontBaseline/v1` | PresentationTokens.cs；Profile §3.4 命名/schema 化完全一致 |
| ViewportGeneration | `long ≥ 1`，视口适配器单调代 | ViewportGenerationTracker（Interlocked 递增，Volatile 读）；非墙钟/随机；每视口私有 |
| SceneAssemblerVersion | `int ≥ 1`（M1=1） | 装配器版本一致性校验（Assembler :50-55） |

不参与指纹的排除项核实：无进程 ID/线程 ID/时间戳/随机数/对象地址/可变引用（Canonicalizer 全源码核对）；快照诊断 `UtcAssembledAt` 存在于快照但**不参与指纹**（构造期指纹先于诊断生成，且诊断不在序列化布局中）。

## 3. ContextFingerprint（任务 §四.C）

1. **SHA-256 正确性** ✓：`ContextFingerprint.Compute` = `SHA256.HashData(canonicalData)`（ContextFingerprint.cs:25-26）；32 字节私有副本、按字节比较、64 位小写十六进制。
2. **Canonical 序列化确定性** ✓：固定字段顺序、手写定长小端整数（不依赖平台字节序/Dictionary 遍历）、UTF-8 + int32 LE 长度前缀字符串（UTF-8 单射 + 长度前缀框架化 → 无字段边界歧义）、存在标志字节、类别字节。
3. **Schema version 参与编码** ✓：布局首字段 `[int32 LE] SchemaVersion=1`（Canonicalizer :55；测试断言字节头）。
4. **全字段参与** ✓：见字节布局（:54-71 注释与实现一致）——全部 M1 身份字段逐项在列。
5. **编码歧义分析（语义碰撞）**：
   - 字符串：长度前缀 + UTF-8 单射 → 不同字符串必产生不同字节。
   - typed 态：类别字节 + 负载结构差异 → NotEvaluated/Evaluated 任意值、NotConfigured/Configured 任意值两两可区分（测试锁定）。
   - BranchId 三态：null（标志 0x00）/ Guid.Empty（0x01+全零）/ 其他（0x01）字节互异（测试断言长度差 16 字节）。
   - 定长整数：无前导/截断歧义。
   - **未发现任何不同语义状态映射到相同编码的路径**。
6. **不稳定序列化顺序**：不存在——无字典/哈希集遍历、无并行拼接、无文化相关格式化（`ToString` 不参与序列化）。
7. **Pinned 向量覆盖度**：单一固定上下文，但**全部 10 个身份字段均非默认值且显式出现在布局中**——任何编码规则变化（字段顺序/编码宽度/标志值/类别值/schema 值）都会改变哈希使测试失败。审查者独立复算 MATCH 证明"文档布局 = 实现输出 = 测试锁定值"三方一致。限制（非缺陷）：向量不锁定"未来新增字段的追加位置"，这由 schema 版本纪律约束（编码规则变化必须递增 SchemaVersion——已在 Canonicalizer 文档注释中约定）。
8. **错误缓存命中风险**：M1 无指纹→快照缓存表；指纹仅用于请求身份校验与装配校验——即使假想碰撞也需 SHA-256 碰撞，超出威胁模型；语义层碰撞已排除（见 5）。

## 4. SceneSnapshot 不可变性（任务 §四.D）

- **真不可变**：`ImmutableArray<SceneLayer>` 私有字段（SceneSnapshot.cs:37）；转型 `ICollection<T>` 写回抛 `NotSupportedException`（测试 Add/Clear/Remove 三路径）；`IReadOnlyList` 背后不暴露 List/数组（`Assert.Null(layer.Items as List<SceneItem>)`）。
- **外部无法修改**：无公开 setter、无集合暴露可变引用、构造函数 internal（仅同程序集 SceneAssembler 可构造）；`SceneSnapshotDiagnostics` 为 init-only record，构造后不可变。
- **同一构建结果**：快照仅在 `SceneAssembler.Assemble` 尾部一次性构造（SceneAssembler.cs:134），指纹/上下文/版本/图层同时注入——不存在部分构建成功、部分字段后改的路径。
- **无越界内容**：类型层面反射测试证明无 Bounds/Extent/Width/Height 属性；无 Avalonia/Skia/DB/ViewModel/会话/闭包/无主缓冲（类型引用图核对）；全部场景项均为 `BlankBackgroundItem`（测试断言）。无相机、无 Map bounds、无伪造几何 ✓（Profile §5.2/§5.3）。

## 5. Publisher（任务 §四.E）

- **发布语义**：`TryPublish` 六步（disposed → candidate 非空 → 指纹==期望 → 序号==活动序号 → 版本>已发布 → `Interlocked.Exchange`），全部在 `_gate` 锁内串行（单写者=协调器；`TryPublish`/`ActivateRequest` 为 internal，外部无法进入）。`Interlocked.Exchange` + 读者 `Volatile.Read` 构成正确的引用发布内存屏障（快照字段全部 readonly 于构造期赋值，经交换的 release 语义安全可见）。
- **读者一致性**：读者一次 `Volatile.Read` 捕获引用后全程复用；发布不改变已捕获引用（测试）；渲染路径零锁/零等待/零准备工作 ✓（PRA-01 §6.3/6.5）。
- **版本单调**：`AllocateCandidateVersion` 在同一 `_gate` 内递增；`_publishedVersion` 只在成功发布时更新；被拒候选留缺口不复用；不存在并发发布版本倒退（版本分配与发布校验同锁）。
- **close/shutdown 后发布**：disposed → `publisher_disposed`/版本分配抛异常/`ActivateRequest` 忽略。**窄竞态窗口**：协调器已关闭但发布器未释放时，已在关闭前通过完成门的请求仍可发布——完整论证与影响评估见 [CONCURRENCY_AND_LIFECYCLE_REVIEW.md §6](CONCURRENCY_AND_LIFECYCLE_REVIEW.md)（FINDINGS F-1，P2）。
- **rejection reason 全路径赋值** ✓：`TryPublish` 的 out 参数在每个失败分支均经 `Reject()` 赋值（曾有的遗漏已由测试捕获修复——交付文档 §5 如实披露，当前代码核实无遗漏路径）。
- **发布失败不污染当前快照**：所有失败路径在交换前返回，`_current` 不变（测试逐路径断言）。

## 6. Coordinator

- **职责**：请求序号、激活（取代旧请求 + 语义清空判定）、准备委托注入、完成门、装配编排、发布委托、有界诊断（计数器 + 64 容量环形缓冲，旧→新副本）。不含绘制、不含跨视口协调、不含通用调度器/队列/事件总线（Profile §6.2 禁令逐项核对通过）。
- **锁序**：协调器 `_gate` → 发布器 `_gate` 单方向（激活路径嵌套；`TryPublish` 只取发布器锁；`Record`/`CloseAsync` 只取协调器锁）——无反向获取，无死锁可能。
- **单写者假设在真实调用路径成立**：`TryPublish` 唯一调用点是 `PrepareAndPublishAsync`（单任务线性流）；`ActivateRequest` 唯一调用点是 `RequestSceneAsync` 激活段（协调器锁内）——注释声明与实际调用图一致（审查者全调用点 grep 核实）。
- **诊断**：`RecentOutcomes` 返回副本（不暴露内部环形数组）；结果含序号/指纹/MapId/BranchId/时间文本/终态/诊断/版本/时刻——不记录地图内容（PRA-01 §9 隐私边界）；有界（64）不随请求量增长（测试灌入 84 请求验证封顶）。

## 7. 结论

架构边界、数据链路、身份机制、不可变性与发布机制**全部符合** PRA-01 V0.1 与 M1 Profile V1 的约束；未发现命名与实现不符、绕边界、伪造语义态或不可变性缺口。唯一架构级保留意见是 F-1（关闭语义的绝对声明存在窄竞态例外），不影响 M1 正确性，但应在 WP-30B 前以小修复关闭以恢复声明的精确性。
