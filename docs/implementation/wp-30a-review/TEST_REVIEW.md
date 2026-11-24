# WP-30A 测试审查（Test Review）

## 1. 实际执行结果（审查者本机，2026-11-24）

环境：Windows 10.0.26300 x64，.NET SDK 11.0.100（global.json 钉扎 MTP runner）。本机 `dotnet test`（sln）存在与 master 相同的既知 MTP NamedPipe 问题，按仓库既有惯例与交付一致的等效方式——从 `WorldView/` 目录直调测试 DLL（`dotnet exec <test.dll>`）执行。

| 工程 | Debug | Release |
|---|---|---|
| WorldView.Domain.Tests | 266 通过 | 266 通过 |
| WorldView.Application.Tests | 137 通过 | 137 通过 |
| WorldView.Infrastructure.Tests | 318 通过 | 318 通过 |
| WorldView.Desktop.Tests | 11 通过 | 11 通过 |
| WorldView.Presentation.Tests | 104 通过 | 104 通过 |
| **合计** | **836 / 836，0 失败 0 跳过** | **836 / 836，0 失败 0 跳过** |

- 构建：Debug 与 Release 全 solution 均 0 错误；`WorldView.Presentation.csproj` 单独构建 **0 警告 0 错误**（solution 级警告全部来自既有工程，如 Application 的 CTJ001 分析器警告——交付声明属实）。
- 并发稳定性：Presentation 测试 Release 连续 5 次重复执行全部通过（无 flake）。
- CI：PR #27 两轮 run 全绿——45309401459（`846d072`，交付文档引用）与 45310082928（最终 HEAD `db31e72`）各四 job（windows/macos × Debug/Release）SUCCESS。**CI 为 headless 构建+测试证据，不构成 macOS GUI 验证**（交付文档对此边界的声明准确）。

## 2. 测试有效性逐项核对（任务 §四.I）

### 2.1 确定性手段（无时序依赖）

- **`Thread.Sleep`：零调用**（grep 唯一匹配出现在注释"绝不用 Thread.Sleep"中）。
- 交错控制全部为 `TaskCompletionSource` 门且带 `TaskCreationOptions.RunContinuationsAsynchronously`（两处门定义 + 停止信号均核实）——续体不内联运行，`TrySetResult` 后断言不会因同步续体产生假时序。门模式固定为"先设门 → 发起请求 → `WaitForEnteredAsync()` 锚定 → 定向 `Release()`"，消除了"门后设导致请求抢先完成"类竞态（交付 §5 披露的修复属实）。
- 唯一 `Task.Delay(1)` 在 `ConcurrentReaders` 压力循环中作步进节流；其校验的不变量（单引用内自洽）与交错顺序无关——不属于时序依赖。判定：**该压力测试为真实性加分项而非时序脆弱点**。
- 文化/时区测试置于 `DisableParallelization` 专用 collection（文化是进程级状态）——正确。

### 2.2 "只检查返回值、不检查最终发布状态"的测试？

不存在。协调器全部 14 项测试在断言 outcome 之外**均核验 `Publisher.Current` 的终态**（指纹/版本/null/Same 引用）；发布器全部测试核验 `Current`/`PublishedVersion`/拒绝计数。发布结果与读者可见状态一致是被系统断言的对象。

### 2.3 关键语义覆盖矩阵

| 要求场景 | 覆盖测试 | 判定 |
|---|---|---|
| 取消不生效仍正确 | `CancellationNotHonoredByWorker_CorrectnessStillHolds`（E9：准备函数无视已取消令牌并产出完整结果 → 仍按请求身份 Superseded） | ✓ 覆盖 |
| 错误 fingerprint | 装配拒绝（`MismatchedFingerprint_Rejected`、`AssemblyRejection_ClassifiedWithStage`）+ 发布器 `WrongContextCandidate_IsRejected` + 准备盖错戳（`MarkMismatchedStamp`）三层 | ✓ 覆盖 |
| 关闭后迟到请求 | `CloseWithInFlightRequest_LateCompletionCannotPublish`（E7）+ 关闭后新请求拒绝 | ✓ 覆盖（确定性路径；竞态例外见 F-1，无法用现门控确定性构造） |
| shutdown | `Shutdown_RejectsEverything_NotYetCompleted`（E8）+ `Dispose_RejectsAllFurtherPublication_ButCurrentStaysReadable` + `ActivateRequest_AfterDispose_IsIgnored` | ✓ 覆盖 |
| last-known-good | `BuildFailure_KeepsSemanticLastKnownGood`（E5）+ `SameSemanticRefresh_RetainsLastKnownGood` + 语义切换清空/不复活/切回恢复（E6） | ✓ 覆盖 |
| 真实 Application/Domain 链路 | `Cd01EndToEndTests.FullChain` 断言强化（真实 SQLite + 真实 Domain 解析，无端口替身）→ RootHistoryId/typed 时间保留；`ValidatedWorkspaceContextMappingTests` 会话层；`PresentationContextFactoryTests` 映射层 | ✓ 各跳分别覆盖（组合链的"e2e 解析结果直接进工厂"无单一测试，见 §4 缺口清单） |
| 读者一次捕获 | `ReaderCapturesReferenceOnce_ReferenceRemainsValidAndConsistent` + 并发读者压力（0 混淆） | ✓ 覆盖 |
| 版本单调/超替/错上下文/先指纹后序号后版本 | 发布器测试逐项 + 检查顺序经构造性场景锁定 | ✓ 覆盖 |
| 视口隔离 | `IndependentViewports_DoNotSupersedeOrOverwriteEachOther` | ✓ 覆盖 |
| 有界诊断 | 计数器断言 + 环形缓冲灌入 84 请求封顶 64 + 落定顺序 `[1,2,3,5,4]` 结构性证据 | ✓ 覆盖 |
| 不可变性 | 转型写回抛异常（Add/Clear/Remove）/源篡改不渗透/不暴露 List/数组/反射无 bounds 属性/无虚构内容 ID | ✓ 覆盖 |
| 指纹全字段敏感性 | MapId/BranchId(null↔值)/RootHistoryId/时间 day/offset 1 秒/代数/Style/Font/装配版本/两类语义态互异 | ✓ 覆盖 |
| pinned 向量 | `PinnedSha256Vector_MatchesCanonicalSerialization`；审查者独立复算 MATCH | ✓ 覆盖 |

### 2.4 "测试通过但没有实际执行关键代码路径"？

未发现。特别注意两点：(a) 拒绝路径测试均断言 `rejectionReason`/`RejectionStage` 具体值——曾有的 out 参数遗漏赋值缺陷正是被此类断言捕获（交付 §5 披露，当前代码核实全部拒绝路径均赋值）；(b) E10 的逆序放行真实执行了四个晚完成请求的 Superseded 路径，而非仅断言最新发布。

## 3. 数量核对

- 全仓库 836 = 基线 728 + 108（Presentation 104 + 映射聚焦 4）✓ 与交付一致。
- Presentation 104 = [Fact] 96 + Theory 展开用例 8（`ViewportGeneration_MustBePositive` 2 + `SceneAssemblerVersion_MustBePositive` 2 + Context 校验 Theory 等）；各文件分布：Assembler 18 / Fingerprint 18（含文化串行 2）/ Context 20 / Publisher 12 / Coordinator 14 / Snapshot 8 / Factory 9 / Boundary 2（运行器口径合计 104 ✓）。

## 4. 覆盖缺口（不含已列 FINDINGS 的重复项）

1. **close↔dispose 窗口竞态**（F-1）：现有门控测试天然覆盖不到（门挂起点在关闭前）。建议后续以内部钩子或重构发布决策点使该路径可确定性构造。
2. **e2e→工厂组合跳**：真实解析链（Infrastructure e2e）与工厂映射（Presentation 单元）分别有测试，但无单一测试把 e2e 产出的 `ValidatedWorkspaceContext` 直接喂给工厂。两跳均为纯映射、字段同构，风险低——建议级。
3. **同上下文刷新中"调用方取消但准备函数完成 → 仍发布"**（合法且正确的行为）无显式测试；E9 覆盖其超替变体。建议级。
4. **`TryFromHex` 非法输入**仅测一个样例（"zz"）；非权威路径，建议级。

以上缺口均不构成 Profile §8 测试矩阵的未满足项——矩阵要求的十二行全部有对应测试（逐行核对见 §2.3）。

## 5. 交付文档声明准确性抽查

| 声明 | 核对结果 |
|---|---|
| 836/836 Debug+Release、Presentation 0 警告、零包版本变化 | 属实（本机复测） |
| CI run 45309401459 四 job 全绿 | 属实；另有最终 HEAD 上的 run 45310082928 亦全绿（文档提交于两 run 之间，属正常时序） |
| "无 Thread.Sleep 时序依赖" | 属实（唯一匹配为注释） |
| 本机 MTP NamedPipe 以直调 DLL 等效完成 | 属实（审查者复现同一现象并用同一等效方式） |
| TEST_REPORT 协调器"15 项"/发布器"11 项" | 与 [Fact] 实数 14/12 有 ±1 偏差（口径差异，总数 104 无误）——已记入 FINDINGS F-5 |
| CONTEXT_AND_FINGERPRINT "文化/时区无关（含时区变化）" | 文化部分属实；时区测试实际未切换 `TimeZoneInfo.Local`（F-3）——不变量本身由编码结构保证，不失效 |
| SNAPSHOT_LIFECYCLE §5.3 "协调器关闭/发布器释放后的任何完成路径一律 RejectedClosed" | 对发布器释放属实；对协调器关闭存在 F-1 窄竞态例外（窗口止于发布器释放） |
