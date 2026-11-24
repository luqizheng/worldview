# WP-30A 并发与生命周期专项审查（Concurrency & Lifecycle Review）

审查方法：对任务 §四.F 列出的九类时序逐一路径推演（引用实际代码行），并与确定性测试的覆盖对照；随后审查原子发布、last-known-good、close/shutdown。**取消令牌在任何正确性论证中均不被当作保障**——此纪律在实现与测试中均得到遵守。

代码位置缩写：`Coord` = `PresentationRequestCoordinator.cs`；`Pub` = `SceneSnapshotPublisher.cs`；`Runtime` = `ViewportPresentationRuntime.cs`。

## 1. 九类时序逐项分析（任务 §四.F）

### F-1 时序：A 先发、B 后发、B 先完成、A 后完成
- 路径：A 激活（seq=1，`Pub.ActivateRequest(1,…)`）→ B 激活（seq=2，`previous.MarkSuperseded()`，`Coord:170-176`）→ B 完成门通过 → 装配 → `TryPublish(snapshotB, 2)`：指纹==期望、序号==活动序号 → 发布 v1 → A 完成：`request.IsSuperseded == true`（Volatile 写于 MarkSuperseded）→ `Superseded`（`Coord:279-287`）。即使 A 的完成门检查在 B 激活前通过（竞态），`TryPublish` 的序号检查（`Pub:163-167`，发布器锁内）仍拒绝 `stale_request`——**双重防线**。
- 正确性由请求身份 + 发布器当前活动序号共同保证，与取消无关 ✓。测试：`SlowOldRequest_CannotOverwriteNewerRequest`（E1-E3，门控确定性）。

### F-2 时序：A 执行期间 viewport generation 变化
- 变化 → 新请求 B（新指纹，同语义身份）→ B 激活取代 A → 同 F-1。语义身份不变 → **不清空** last-known-good ✓。测试：`RapidGenerationChanges_OnlyLatestValidResultPublishes`（E10：gen1..4 挂起、5 发布、1..4 依次放行全部 Superseded）。

### F-3 时序：A 执行期间 Map/Branch 变化
- 新请求 B（不同 Map/Branch → 语义身份不同）→ B 激活时 `Pub.ActivateRequest` 判定语义切换 → `Interlocked.Exchange(ref _current, null)` 原子清空（`Pub:120-126`）→ A 完成门：`IsSuperseded` → Superseded；竞态穿透完成门时 `TryPublish` 指纹检查拒绝（期望指纹已是 fpB）✓。旧上下文迟到结果**不可能重新发布**。测试：`SemanticContextSwitch_OldSnapshotCannotMasqueradeAsNewContext`（E6，含准备期间读者已见 null 的断言）。

### F-4 时序：A 执行期间 HistoricalTime 变化
- 同 F-3：`WorkspaceSemanticIdentity.Equals` 精确比较 `HistoricalTime`（DayNumber+OffsetSeconds，Domain 值相等语义）——不同时间必判语义切换。测试：`TimeOnlySemanticChange_SameMapBranch_IsSemanticSwitch`（同 Map/Branch、不同时间 → 清空）✓。无"不同时间误判同上下文"路径。

### F-5 时序：请求被取消但底层不响应取消
- 取消仅经链式 CTS 协作传递；准备函数无视令牌照常产出完整结果时：完成门/发布器序号检查仍按请求身份拒绝（E9 锁定）✓。取消从不作为正确性机制（代码注释与实现一致）。

### F-6 时序：请求构建失败，随后旧请求完成
- "旧请求"此刻必已被新请求取代（否则不是旧请求）→ F-1/F-3 路径拒绝 ✓。若指同语义刷新失败：失败结果 `Failed`，last-known-good 保留（`BuildFailure_KeepsSemanticLastKnownGood`，E5；发布器级 `SameSemanticRefresh_RetainsLastKnownGood`）✓。

### F-7 时序：viewport 已关闭，旧请求才完成（确定性路径）
- `CloseAsync`：`_closed=true` + 取消活动请求 + `_closeCts.Cancel()`（`Coord:367-382`）→ 迟到完成：完成门读 `closed==true` → `RejectedClosed`，不可发布（E7）✓。
- **窄竞态例外（F-1 发现，P2）**：完成门检查（`Coord:268-277`）与 `TryPublish`（`Coord:316`）之间不是同一原子区。交错：R 在关闭前通过完成门（读得 closed=false）→ `Runtime.DisposeAsync` 执行协调器关闭（`_closed=true`）→ R 尚未到达 `TryPublish`，且发布器尚未释放（Runtime 在协调器 DisposeAsync 之后才 `Publisher.Dispose()`，`Runtime:56-60`）→ R 的 `TryPublish`：`_disposed==false`，指纹/序号仍是当前值（关闭后无新请求，期望指纹未变）→ **发布成功**。窗口止于发布器释放（`publisher_disposed` 是最终屏障）。详见 FINDINGS F-1。

### F-8 时序：application shutdown 后多个请求依次完成
- `Runtime.DisposeAsync`：协调器关闭 → 发布器释放。之后：新请求 → `RejectedClosed`（`Coord:143-146` 检查 `_publisher.IsDisposed`）；在途请求完成门 → `RejectedClosed`；版本分配 → `InvalidOperationException` → `RejectedClosed`（`Coord:291-299`）；直接 `TryPublish` → `publisher_disposed`。多请求依次完成全部拒绝，无状态污染；已发布快照仍可读（PRA-01 §8 语义，测试 `Dispose_RejectsAllFurtherPublication_ButCurrentStaysReadable`）✓。测试：`Shutdown_RejectsEverything_NotYetCompleted`（E8）、`ActivateRequest_AfterDispose_IsIgnored_NoClearNoPublish`。

### F-9 时序：多次快速请求，完成顺序与提交顺序完全相反
- E10 即此场景的完全逆序（1..4 后完成、5 先发布）：每次新激活立即取代前一个未发布请求；发布仅可能属于 seq=5；1..4 无论以何种顺序放行均为 Superseded ✓。诊断环形缓冲记录落定顺序 `[1,2,3,5,4]` 作为结构性证据（`Diagnostics_CountersAndBoundedRingBufferReflectOutcomes`）。

**综合判定**：正确性确由 Request identity + ContextFingerprint + Viewport lifecycle + 当前 generation（活动请求序号）共同保证；唯一例外是 F-7 的窄竞态窗口（P2）。

## 2. Publisher 原子性细节（任务 §四.E 补充）

- `Current` = 纯 `Volatile.Read`（`Pub:39`）；唯一写入 = `Interlocked.Exchange`（`Pub:176`，且在锁内——锁保证单写者，交换保证原子性与全屏障）。消费者不可能在一次读取中获得"半个快照"——引用读取是原子的，快照自身不可变。
- 版本严格单调：分配与校验同锁（`Pub:87-100` / `Pub:169-177`）；并发发布不存在（单写者），即使假想多写者也受锁+版本检查双重保护。
- 语义清空的原子性：清空发生在 `ActivateRequest` 的发布器锁内——读者只会看到旧快照或 null，绝无混合状态；`ContextSwitchClearCount` 仅在真的清空时递增。
- 单写者假设核实：`TryPublish`/`ActivateRequest` 为 internal，唯一调用方是同程序集协调器的单任务线性流（调用图全量 grep 核实）——**注释中的假设与真实调用路径一致**。

## 3. Last-known-good 与语义切换（任务 §四.G）

1. **四元组比较完整性** ✓：`WorkspaceSemanticIdentity` = (MapId, BranchId, RootHistoryId, HistoricalTime) 精确 typed 相等——覆盖 Profile §6.4 全部维度并额外含 RootHistoryId（Profile §3.1/§6.1 身份字段）。
2. **不同 HistoricalTime 误判同一上下文？不可能**：`HistoricalTime.Equals` 按值比较（Domain），测试锁定秒级差异即切换。
3. **清空与新请求之间的可见窗口**：清空在激活时同步发生（准备开始前）；窗口内读者见 null → 语义上是"加载态"，不是旧上下文误显——这是交付选择的 §6.4 "clear" 分支的预期表现，不存在新旧混合窗口 ✓。
4. **旧上下文迟到结果重新发布？不可能**：指纹（期望已切换）+ 序号（已非活动）双重拒绝；E6 覆盖。
5. **新上下文构建失败保持空态** ✓：E6 断言失败后 `Current == null` 且旧上下文不复活。
6. **同上下文刷新失败错误清空？不可能**：清空条件是语义身份变化；仅代数/token/装配版本变化属同语义（指纹变化但语义身份不变）→ 保留 last-known-good ✓（E5 + 发布器级测试）。

**策略符合性判定**：符合 Profile §6.4——"clear or replace" 二选一中选择了 clear（原子清空），且严格区分两种失败场景；该选择已在交付文档中作为显式 presentation policy 声明。审查无异议。

## 4. Lifecycle（任务 §四.H）

全链路：创建（Runtime.Create 三件套）→ 请求创建（序号+链式取消源，finally 释放）→ 执行（准备/装配）→ 完成（完成门分类七种终态）→ 发布（六步校验+原子交换）→ 读取（Volatile.Read 单引用捕获）→ 关闭（CloseAsync 幂等；停新、取消在途）→ 停机（DisposeAsync 先协调器后发布器，幂等；一切发布/分配/新请求拒绝）。

- **关闭后状态改变**：确定性路径无（E7/E8）；窄竞态例外见 F-1。已发布快照在被引用期间保持有效（设计允许，非污染）。
- **对象引用/未完成任务**：请求任务完成后 finally 释放取消源并清理 `_activeRequest`（仅当仍是自身）；`MarkSuperseded` 对已释放取消源安全（ObjectDisposedException 捕获）；无句柄泄漏路径。
- **WP-30B 渲染资源接入面**：M1 无 GPU 资源，但生命周期边界已按"后端资源归渲染管线、不进快照、释放序列先协调器后发布器"建立——WP-30B 可在 `ViewportPresentationRuntime` 的关闭边界上挂接 Skia 资源释放（读者侧持有的快照引用不受影响）。前置事项清单见 [WP-30B_READINESS.md](WP-30B_READINESS.md)。
- 一个小的诊断分类 nuance（F-2，P3）：被取代的请求若其准备函数抛出**非取消类**异常，落定结果记为 `Failed`/preparation-failure 而非 `Superseded`——正确性不受影响（Failed 同样不可发布），仅诊断计数口径与文档表述存在偏差。
