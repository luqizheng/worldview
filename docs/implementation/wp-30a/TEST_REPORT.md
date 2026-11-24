# WP-30A 测试报告

## 1. 汇总

| 项目 | 测试数 | Debug | Release |
|---|---:|---|---|
| WorldView.Presentation.Tests（新增） | 104 | 全部通过 | 全部通过 |
| WorldView.Application.Tests（含新增 4 项映射测试） | 137 | 全部通过 | 全部通过 |
| WorldView.Infrastructure.Tests（含 e2e 新增断言） | 318 | 全部通过 | 全部通过 |
| WorldView.Domain.Tests | 266 | 全部通过 | 全部通过 |
| WorldView.Desktop.Tests | 11 | 全部通过 | 全部通过 |
| **合计** | **836** | **0 失败** | **0 失败** |

基线对照：master @ c05ca48 为 728 测试；本阶段新增 108（104 Presentation + 4 映射聚焦测试；
e2e 断言为既有测试的强化，不计新增数量）。

## 2. 类别映射（任务 §十 / Profile §8 测试矩阵）

### A. Context（`PresentationContextTests`，20 测试）
合法构建与身份保持（Map/Branch/RootHistory/typed 时间 day+offset 精度）；null Branch = 根历史；
必需字段缺失 fail-closed（空 MapId/RootHistoryId、null 时间/语义态、空白 token、非法代数/
版本）；WorldStateRevision 恒 NotEvaluated；Projection 恒 NotConfigured；Style/Font token
精确匹配；值相等语义；Evaluated/Configured 构造校验。

### B. Fingerprint（`ContextFingerprintTests`，17 测试）
相同上下文跨构建点指纹相同；**pinned SHA-256 向量**（`8d59a564…`，锁定全部编码规则）；
32 字节/64 hex/Hex 重建；每个身份字段独立变化改变指纹（含历史时间偏移 1 秒精度）；
NotEvaluated vs Evaluated 可区分（含不同 Evaluated 之间）；NotConfigured vs Configured
可区分；canonical 字节逐字节稳定 + schema 版本头；BranchId 缺省 vs 空 GUID 区分
（+16 字节布局）；字段相等 ⇔ 指纹相等；文化（tr-TR/ar-SA）与时区无关（独立串行
collection，禁并行）；canonical 时间精度保留。

### C. SceneSnapshot（`SceneSnapshotTests`，8 测试）
空白场景结构（单背景层/单背景项/稳定 ID/token）；快照与上下文指纹一致 + 版本/装配版本
一致；**真正不可变**（ImmutableArray 转型写回抛 NotSupportedException：Add/Clear/Remove）；
源集合构建后篡改不渗透；反射证明不存在 Bounds/Extent/Width/Height 属性；无虚构几何
（全部场景项均为 BlankBackgroundItem 且 ID 不含 road/building/terrain/label/decoration）；
多次组装确定性一致且不改 Domain 值；图层背后不暴露 List/数组。

### D. Publisher（`SceneSnapshotPublisherTests`，11 测试）
首次发布（null → v1）；读者一次捕获引用、发布后旧引用内容自洽不变、Current 原子切换；
版本严格递增；过旧/等版本候选拒绝（non_monotonic）；指纹不符候选拒绝（context_mismatch，
在检查顺序上先于版本检查——测试按此构造）；超替请求候选拒绝（stale_request）；语义切换
原子清空（含同 Map 不同历史时间）；同语义刷新保留 last-known-good；Dispose 后发布/分配
全拒绝但已发布快照仍可读；并发读者持续捕获校验（0 混淆）。

### E. Request coordinator（`PresentationRequestCoordinatorTests`，15 测试）
首发布 v1；**慢旧请求不可覆盖新请求**（A 挂起 → B 完成发布 → A 晚完成 Superseded，
A/B 指纹不同）；同语义构建失败保留 last-known-good；**语义切换**旧快照原子清空、失败后
不复活、切回后恢复；**关闭后迟到完成不可发布**（RejetedClosed）；**停机后一切不可发布**
（含候选版本分配拒绝）；**取消不生效仍保持正确性**（准备函数无视已取消令牌，仍按请求
身份拒绝）；调用方中途取消 → Cancelled 不发布；发起前已取消不激活不取代；**快速多次
generation 变化仅最新有效结果发布**（1..4 挂起、5 立即发布、依次放行 1..4 全部 Superseded）；
**视口隔离**（独立序号/版本/计数，互不取代）；诊断计数与有界环形缓冲（含"结果落定顺序
[1,2,3,5,4] 恰证晚完成不覆盖"）；装配拒绝带 RejectionStage 分类；结果携带上下文身份。

### F. Application mapping
- `ValidatedWorkspaceContextMappingTests`（Application.Tests，4 测试）：会话打开保留
  RootHistoryId + typed 时间；null 输入 → typed 纪元；SwitchContext 替换两项身份；canonical
  文本由 typed 值计算（前导零输入规范化），无第二事实来源。
- `Cd01EndToEndTests.FullChain`（Infrastructure.Tests，断言强化）：真实 SQLite + 真实
  Core Domain 解析链路（无端口替身）→ RootHistoryId == map.RootHistory.HistoryId、
  typed 时间 == `new HistoricalTime(1300, 43200)`——验证实际架构边界而非 DTO 赋值。
- `PresentationContextFactoryTests`（Presentation.Tests，9 测试）：映射不丢身份（含
  `Assert.Same` 证明不经字符串往返）；fail-closed（null 上下文/空 RootHistoryId/非法代数）；
  恒 NotEvaluated/NotConfigured（不伪造）；token/装配版本正确；同输入同指纹。
- `PresentationBoundaryTests`（2 测试）：程序集引用仅 Application+Domain（方向单向），
  无 Avalonia/Skia/Sqlite/EF/Infrastructure/Desktop 引用；公开 API 不暴露违禁技术栈类型。

## 3. 确定性异步手段

- 全部交错控制使用 **TaskCompletionSource**（`RunContinuationsAsynchronously`）门：
  `Gate(sequence)` 先设门、后发起请求，`WaitForEnteredAsync()` 锚定"请求已进入准备"，
  `Release()` 定向放行——无任何 `Thread.Sleep` 时序依赖。
- 唯一的 `Task.Delay(1)` 出现在并发读者压力循环中，仅作步进节流（不变量与交错顺序无关），
  非时序依赖。
- 文化/时区测试置于 `DisableParallelization` 专用 collection（文化是进程级状态）。
- 修复过程中曾出现两类测试自身缺陷并已纠正（见 IMPLEMENTATION_SUMMARY §5）：
  门后设导致挂起竞态、门释放前阻塞读取 `task.Result` 死锁。

## 4. 未覆盖项（显式声明）

1. **Avalonia/Skia 渲染**：无渲染消费者（GAP-D），读者契约以测试替身验证——真实控件
   单引用捕获属 WP-30B/PRA-02（Profile §8 明确允许）。
2. **macOS 本机**：本机为 Windows；macOS 仅由 CI 四平台矩阵覆盖（无 GUI 证据，
   见 PLATFORM_VALIDATION.md）。
3. **真实 World State 读模型接入**：`Evaluated`/`Configured` 仅类型预留 + 指纹可区分性
   测试，无读模型集成测试（上游不存在）。
4. **长时并发压力/性能**：本阶段正确性优先，无基准测试（Profile §7 out of scope）。
5. 代码覆盖率工具未运行（仓库既有测试惯例未含 coverage 门禁；xunit.v3 + coverlet collector
   已随模板保留）。
