# WP-30A Definition of Done 逐项验收（Profile §9）

审查对象：PR #27 @ `db31e72`（代码 `9e6d777`）。每项只使用 PASS / FAIL / PARTIAL / NOT VERIFIED，并附代码或测试证据。全部证据来自审查者实际读码与实际执行，不采信交付文档自述。

## DoD-1 — Presentation 与 Presentation.Tests 纳入 solution 与 CI build/test 流

**PASS**

- `WorldView/WorldView.sln` diff（`c05ca48..9e6d777`）：两工程注册 + src/tests 解决方案文件夹 + 六种配置矩阵（Any CPU/x64/x86 × Debug/Release）。
- CI workflow 以 sln 级 build/test 运行：run 45310082928（最终 HEAD `db31e72`）四 job（windows/macos × Debug/Release）全部 SUCCESS；run 45309401459（`846d072`，交付文档引用）同样四 job SUCCESS。
- 本机实测：`WorldView.Presentation.Tests` Debug/Release 各 104 项独立执行通过。

## DoD-2 — Application 校验上下文保留 typed HistoricalTime 与 RootHistoryId

**PASS**

- `IMapQueryService.cs`：`ValidatedWorkspaceContext` 新增 `required Guid RootHistoryId` 与 `required HistoricalTime HistoricalTime`（`WorldView.Domain.Time` typed 类型）；`CanonicalHistoricalTime` 改为由 typed 值计算的计算属性——消除字符串/typed 双事实来源。
- `CoreDomainAdapters.cs`（Infrastructure 映射）：`RootHistoryId = context.RootHistoryId, HistoricalTime = context.Time`，不再丢弃。
- 三层测试证据：
  - `ValidatedWorkspaceContextMappingTests`（4 项）：会话打开/SwitchContext/null→纪元/前导零规范化；
  - `Cd01EndToEndTests.FullChain` 断言强化：**真实 SQLite + 真实 Domain 解析链路**（无端口替身）下 `workspace.Context.RootHistoryId == map.RootHistory.HistoryId`、typed 时间 == `new HistoricalTime(1300, 43200)`；
  - `PresentationContextFactoryTests.Mapping_PreservesMapBranchRootHistoryAndTypedTime`：`Assert.Same(time, context.HistoricalTime)` 证明不经字符串往返重建。

## DoD-3 — World State = NotEvaluated、Projection = NotConfigured，无伪造

**PASS**

- `WorldStateRevision.cs` / `ProjectionDefinition.cs`：abstract record + 私有构造 + 单例语义态（`NotEvaluated.Instance` / `NotConfigured.Instance`）；`Evaluated`/`Configured` 为显式预留类型（身份非空白校验）。
- `PresentationContextFactory.FromValidatedWorkspace` 固定注入两个单例（`PresentationContextFactory.cs:47-48`）——仓库中不存在把时间戳/ID/哈希构造为 revision 的任何代码路径（审查者全源码 grep 核实）。
- 指纹可区分性测试：`NotEvaluated_Vs_Evaluated_AreDistinguishable`、`NotConfigured_Vs_Configured_AreDistinguishable`（含不同 Evaluated/Configured 之间互异）；canonical 类别字节（0x01/0x02）见 `PresentationContextCanonicalizer.cs:42-45`。
- `NotEvaluated` 不抹除历史上下文：MapId/BranchId/RootHistoryId/typed 时间独立完整参与序列化与指纹（§3 字节布局）。

## DoD-4 — Canonical 指纹向量通过且含全部 M1 身份字段

**PASS**

- Pinned 向量测试 `PinnedSha256Vector_MatchesCanonicalSerialization` 锁定 `8d59a564…c7e9`。
- **审查者独立复算 MATCH**（按文档字节布局自写序列化 + SHA-256，与实现及测试三方一致）。
- 全字段敏感性测试齐备：MapId / BranchId（null↔值↔另一值）/ RootHistoryId / 时间（day 与 offset 各 1 秒精度）/ ViewportGeneration / StyleToken / FontToken / SceneAssemblerVersion / NotEvaluated↔Evaluated / NotConfigured↔Configured，逐一 `Assert.NotEqual`。
- 文化无关性测试（tr-TR/ar-SA，专用串行 collection）通过；canonical 时间精度测试通过。
- 注意项（P3，见 FINDINGS F-3）：文化测试声明覆盖"时区"但实际未切换 `TimeZoneInfo.Local`；该不变量在结构上成立（canonical 编码不含任何本地时间 API），故不影响本项 PASS。

## DoD-5 — 空白快照按所有权不可变且携带一致上下文身份

**PASS**

- `SceneSnapshot._layers` / `SceneLayer._items` / `SceneLayerDescriptor._items` 均为 `ImmutableArray<T>` 私有字段（非接口性只读）；`SceneSnapshot` 构造函数为 `internal`——外部程序集无法构造快照。
- 测试证明"真正不可变"：`Snapshot_IsTrulyImmutable_NotJustInterfaceReadonly`（`ICollection<T>.Add/Clear/Remove` 转型写回均抛 `NotSupportedException`）；`MutatingSourceCollections_AfterConstruction_CannotAffectOutputs`（构建后篡改源 `List<T>` 不渗透）；`LayerBackingCollection_IsNotExposedMutableListOrArray`。
- 一致身份：`Snapshot_CarriesCoherentContextIdentity`（Fingerprint==Context.Fingerprint、版本/装配版本一致）；快照仅能由 `SceneAssembler.Assemble` 以整体校验后的输入构造——背景、场景内容、上下文身份、指纹天然来自同一构建结果，不存在部分构建后被改写的路径（无公开 setter、无 init 属性暴露集合）。

## DoD-6 — 装配拒绝不完整或混合上下文必填输入

**PASS**

- `SceneAssembler.Assemble`（`SceneAssembler.cs:32-136`）拒绝路径：context_missing / invalid_snapshot_version / scene_assembler_version_unsupported / required_input_missing / required_input_incomplete / required_input_context_mismatch / required_input_schema_unsupported / required_input_subsystem_unexpected / optional_input_null_element / optional_input_incomplete / optional_input_context_mismatch / optional_input_schema_unsupported；任一拒绝零产出（`SceneAssemblyResult.Rejected` 不产生部分快照）。
- 测试逐类覆盖（`SceneAssemblerTests`，18 Facts）：缺失/失败结果/指纹不一致/schema 不支持/子系统名不符/装配版本不支持/可选出现即须完整有效。

## DoD-7 — 每-视口发布原子、单调、latest-request-wins

**PASS**

- 原子性：`SceneSnapshotPublisher.Current` = `Volatile.Read`（`SceneSnapshotPublisher.cs:39`）；唯一写入点 `TryPublish` 内 `Interlocked.Exchange`（:176），写侧持 `_gate` 锁单写者串行。读者契约测试：`ReaderCapturesReferenceOnce_ReferenceRemainsValidAndConsistent`、`ConcurrentReaders_AlwaysObserveCompleteOldOrNewSnapshot`（0 混淆）。
- 单调性：版本由发布器唯一分配（`AllocateCandidateVersion`，Interlocked 递增，disposed 拒绝）；`TryPublish` 第 4 检查 `candidate.SnapshotVersion <= _publishedVersion → non_monotonic_version`；测试 `SnapshotVersion_StrictlyIncreases`、`OldOrEqualVersionCandidate_IsRejected_LastKnownGoodKept`。
- latest-request-wins：六步校验（disposed → candidate → 指纹 → 请求序号 → 版本 → 交换）；超替/迟到/错上下文三类拒绝均有测试（`SupersededRequestCandidate_IsRejected`、`WrongContextCandidate_IsRejected`、协调器 E1/E9/E10）。
- 外部无法绕过协调器发布：`TryPublish`/`ActivateRequest` 为 internal；`SceneSnapshot` 构造为 internal。

## DoD-8 — 取消、关闭、停机、迟到结果确定性测试

**PASS**

- 全部交错用 `TaskCompletionSource`（`RunContinuationsAsynchronously`）门实现：先设门 → 发起 → `WaitForEnteredAsync()` 锚定 → 定向 `Release()`。审查者 grep 核实：测试源码中 `Thread.Sleep` 零调用（唯一匹配出现在注释"绝不用 Thread.Sleep"中）；唯一 `Task.Delay(1)` 在并发读者压力循环中作步进节流（不变量与交错顺序无关）。
- 覆盖：E9 取消不生效仍正确（准备函数无视已取消令牌，仍 Superseded）；E7 关闭后迟到完成 RejectedClosed；E8 停机（DisposeAsync）后一切拒绝；发起前已取消不激活不取代；调用方中途取消 → Cancelled 不发布。
- 确定性缺口：close 与 publisher dispose 之间的窄竞态窗口无测试（本质上无法用现门控确定性构造，见 F-1）；测试声明与代码声明一致，未虚报。

## DoD-9 — Presentation 程序集无 Avalonia/Skia/数据库/ORM/ViewModel/Dispatcher 依赖

**PASS**

- `WorldView.Presentation.csproj`：唯一 `ProjectReference` = Application；无任何 PackageReference；`InternalsVisibleTo` 仅测试程序集。
- 双轨锁定测试：`PresentationAssembly_HasOnlyApplicationAsProjectReference`（程序集引用白名单 WorldView.Application + WorldView.Domain，方向单向；禁名单 Avalonia/Skia/Sqlite/EF/Dapper/Npgsql/Microsoft.Data/Infrastructure/Desktop/CommunityToolkit）；`PresentationPublicApi_DoesNotExposeInfrastructureOrUiTypes`（公开 API 反射）。
- 审查者独立核对传递链：Presentation → Application → Domain；Application 的 Newtonsoft.Json/Corvus.Text.Json 对 Presentation 仅为传递可用、源码零使用；Desktop 零改动（diff 核实）。

## DoD-10 — Debug/Release 构建与测试通过（net11.0）

**PASS**

- 本机（Windows，SDK 11.0.100，global.json 钉扎）：Debug 0 错误 + Release 0 错误；测试 Debug **836/836**、Release **836/836**（五工程直调 DLL，0 失败 0 跳过）。
- CI：四平台矩阵两轮 run 全绿（45309401459 + 45310082928）。
- 未升级/降级任何 .NET/SDK/Avalonia/Skia 版本（diff 核实：零包变更）。

## DoD-11 — 实施报告记录源文件/API/测试映射/命令结果/偏离/遗留

**PASS**

- `docs/implementation/wp-30a/` 7 文档齐备：README/IMPLEMENTATION_SUMMARY（文件清单+DoD 对照）/ARCHITECTURE（数据链+组件表）/CONTEXT_AND_FINGERPRINT（字段表+字节布局+pinned 向量）/SNAPSHOT_LIFECYCLE（状态机+六步校验+策略表）/TEST_REPORT（836 明细+类别映射+未覆盖项）/PLATFORM_VALIDATION（CI 证据+明确声明无 macOS GUI 验证）。
- 审查者对关键声明逐项核对：836 计数、104/108 新增、Presentation 0 警告、CI run、"本机 MTP NamedPipe 用直调 DLL 等效"均与实际一致；发现 4 处轻微不准确（见 FINDINGS F-1/F-3/F-5，均不影响 DoD 满足）。
- 偏离与遗留已如实披露（§5 实施中修复的缺陷、§6 已知限制 5 项）。

## DoD-12 — 无关工作区内容（尤其 docs/map-editor/implementation/）未触碰

**PASS**

- `git status`：该目录保持未跟踪原样；审查时点仍为会话开始时状态。
- diff 范围（`c05ca48..db31e72`）：仅 sln + Application/Infrastructure 两个源文件 + 既有两测试文件强化 + 新增 Presentation/Test 工程 + `docs/implementation/wp-30a/`——与交付清单逐项一致，无越界改动。
- 本审查自身亦未触碰该目录；审查文档仅新增于 `docs/implementation/wp-30a-review/`。

## 汇总

| # | 要求 | 结论 |
|---|---|---|
| 1 | solution/CI 集成 | PASS |
| 2 | Application 边界保留身份 | PASS |
| 3 | NotEvaluated/NotConfigured 无伪造 | PASS |
| 4 | 指纹向量全字段 | PASS |
| 5 | 快照所有权不可变+一致身份 | PASS |
| 6 | 装配整体拒绝 | PASS |
| 7 | 发布原子/单调/latest-wins | PASS |
| 8 | 生命周期确定性测试 | PASS |
| 9 | 程序集依赖洁净 | PASS |
| 10 | Debug/Release 通过 | PASS |
| 11 | 实施报告 | PASS |
| 12 | 无关内容未触碰 | PASS |

**12/12 PASS，0 FAIL，0 PARTIAL，0 NOT VERIFIED。**
