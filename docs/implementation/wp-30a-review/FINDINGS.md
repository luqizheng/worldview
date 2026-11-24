# WP-30A 审查发现清单（Findings）

审查对象：PR #27 @ `db31e72`（代码 `9e6d777`）。分级：P0 阻断验收 / P1 阻断 WP-30B / P2 非阻断应修复 / P3 建议与硬化。

**汇总：0×P0、0×P1、1×P2、4×P3。**

---

## F-1（P2）协调器关闭与发布器释放之间存在发布竞态窗口

- **严重级别：** P2（M1 无真实读者、最终屏障有效，故非 P0/P1；真实读者进入前的 WP-30B 时点应修复）
- **涉及位置：**
  - `WorldView/src/WorldView.Presentation/Publication/PresentationRequestCoordinator.cs:268-277`（完成门：先读协调器 `_closed`，再读 `_publisher.IsDisposed`——两次独立读取，非同一原子区）与 `:316`（`TryPublish` 调用，其间隔着版本分配与装配 `:289-312`）；
  - `WorldView/src/WorldView.Presentation/Publication/SceneSnapshotPublisher.cs:138-182`（`TryPublish` 六步校验不含协调器关闭状态）；
  - `WorldView/src/WorldView.Presentation/Publication/ViewportPresentationRuntime.cs:56-60`（`DisposeAsync`：先 `Coordinator.DisposeAsync()` 后 `Publisher.Dispose()`，两步之间即窗口）。
- **复现时序（交错）：**
  1. 请求 R 通过完成门（此刻 `_closed==false` 且发布器未释放）；
  2. 视口关闭：`Runtime.DisposeAsync` → 协调器 `CloseAsync` 置 `_closed=true`；
  3. R 尚未到达 `TryPublish`，且发布器尚未执行 `Dispose()`（步骤 2 与释放之间的间隙）；
  4. R 版本分配成功 → 装配成功 → `TryPublish`：`_disposed==false`；指纹==期望（关闭后无新请求激活，期望指纹未变）；序号==活动序号（R 仍是关闭时的活动请求）；版本>已发布 → **发布成功**。
- **实际证据：** 代码路径推演（上列行号）；确定性测试 `CloseWithInFlightRequest_LateCompletionCannotPublish`（E7）的门挂起点在关闭**之前**、释放之后才完成，走的是门内读得 `closed==true` 的确定性分支——该竞态（门在关闭**前**通过）在现有测试构造下不可达，故测试全绿与该窗口存在并不矛盾。
- **可能影响：** M1 无读者 → 无可观察影响；发布的快照属于关闭时的有效上下文（非跨上下文污染），窗口止于发布器释放（`publisher_disposed` 为最终屏障），停机后的最终状态合法。影响是**契约层面的**：交付声明（SNAPSHOT_LIFECYCLE §5.3 "协调器关闭/发布器释放后的任何完成路径一律 RejectedClosed"）与 PRA-01 §8（"shutdown … prevents late publication"）的绝对语义在该窗口内不成立。WP-30B 引入真实读者后，若接线方在协调器关闭与发布器释放之间留有延迟，窗口按延迟放大。
- **建议修复方式（任选其一，改动均为局部）：** (a) 发布决策点纳入协调器关闭状态——如 `TryPublish` 增加由协调器传入的 closed 检查回调，或在发布前于发布器锁内复查协调器关闭标志；(b) 将"完成门检查 + 版本分配 + 装配 + TryPublish"收敛为协调器侧单一再校验点；(c) 最小方案：`Runtime.DisposeAsync` 保持现有紧邻顺序并在文档中把发布屏障明确定义为发布器释放（同时修正 SNAPSHOT_LIFECYCLE §5.3 的绝对表述）。
- **是否需要修改 WP-30A：** 建议是（小修复）；不阻断本次验收，可在 WP-30B 首批改动前/中落地。

## F-2（P3）被取代请求的准备阶段真实异常被诊断为 Failed 而非 Superseded

- **严重级别：** P3（诊断分类口径；正确性不受影响——Failed 同样不可发布）
- **涉及位置：** `PresentationRequestCoordinator.cs:247-255`（通用 `catch (Exception)` 分支不检查 `request.IsSuperseded`；两个 `OperationCanceledException` 过滤器在其之前）。
- **复现条件：** 请求 A 被请求 B 取代（`MarkSuperseded`）→ A 的准备函数抛出非取消类异常（真实失败）→ 落定 `Failed`，计数进入 `PreparationFailures` 而非 `Superseded`。
- **实际证据：** 代码路径；SNAPSHOT_LIFECYCLE §4 状态机将 Superseded 描述为超替请求的落定态，而此路径产生 Failed。
- **可能影响：** 仅诊断计数/环形缓冲的分类口径（超替计数偏低、准备失败计数偏高）；发布行为无差异。
- **建议修复方式：** 通用异常过滤器增加 `when (request.IsSuperseded)` 分支优先归类 Superseded（或文档明确"以异常为准"的口径）。
- **是否需要修改 WP-30A：** 可选（P3）。

## F-3（P3）文化/时区测试声明覆盖时区但未实际切换时区

- **严重级别：** P3（测试声明准确性；被验证的不变量本身成立）
- **涉及位置：** `WorldView/tests/WorldView.Presentation.Tests/Identity/ContextFingerprintTests.cs:211-237`（`CultureAndTimeZoneChange_DoNotAlterFingerprint`：保存 `TimeZoneInfo.Local`、finally `ClearCachedData()`，但从未设置新时区）；交付文档 `CONTEXT_AND_FINGERPRINT.md` §4.1 第 5 条声称"culture（tr-TR/ar-SA）与时区变化不改变指纹"。
- **复现条件：** 读测试体即可。
- **实际证据：** 测试源码。
- **可能影响：** 无功能影响——canonical 编码仅用 DayNumber/OffsetSeconds 定长整数，源码中无任何本地时间/时区 API 参与指纹（审查者全源码核对），不变量由结构保证；但"时区已测"的表述与实际不符。
- **建议修复方式：** 二选一：测试中实际切换时区（`TimeZoneInfo` 在部分平台不可赋值时改用 `TimeZoneUtil`/仅声明结构保证），或将文档表述改为"时区无关性由编码结构保证（不读取本地时区）"。
- **是否需要修改 WP-30A：** 可选（P3，测试/文档措辞）。

## F-4（P3）诊断 ToString 对 default 指纹会越界（当前不可达）

- **严重级别：** P3（防御性硬化；现有代码路径不可达）
- **涉及位置：** `Scene/SceneSnapshot.cs:89` 与 `Identity/PresentationContext.cs:119`（`Fingerprint.ToString()[..12]`）；`ContextFingerprint.ToString()` 对 default 返回 `"default"`（7 字符）→ `[..12]` 将抛 `ArgumentOutOfRangeException`。
- **复现条件：** 需要一个携带 default 指纹的快照/上下文——`ContextFingerprint` 只能经 `Compute` 构造（private ctor），`PresentationContext` 构造期即计算指纹，`SceneSnapshot` internal 构造要求非空上下文 → **当前类型系统下不可达**。
- **实际证据：** 代码推演；全部构造路径核实。
- **可能影响：** 无（不可达）；属未来演进（如序列化往返场景）的潜在陷阱。
- **建议修复方式：** ToString 中以 `Fingerprint == default` 分支或 `Min(length, 12)` 防御。
- **是否需要修改 WP-30A：** 可选（P3 硬化）。

## F-5（P3）交付文档个别计数与措辞与实际有轻微偏差

- **严重级别：** P3（文档准确性；关键声明全部核实属实）
- **涉及位置与偏差：**
  1. `TEST_REPORT.md` §2-E 称协调器"15 项"测试、§2-D 称发布器"11 项"——实际 `[Fact]` 分别为 14 / 12（总数 104 无误，个别分项口径偏差）；
  2. `TEST_REPORT.md` §1 称 Application.Tests"含新增 4 项映射测试"为 137——属实（基线 133+4），但同表将 e2e"断言强化"计为既有测试，口径一致性无问题，仅提醒后续以运行器输出为准；
  3. F-1/F-3 涉及的绝对化表述（SNAPSHOT_LIFECYCLE §5.3、CONTEXT_AND_FINGERPRINT §4.1-5）见上。
- **实际证据：** 文档原文 vs 运行器输出（836/104）与 `[Fact]` 计数。
- **可能影响：** 无功能影响；影响后续审计对账效率。
- **建议修复方式：** 随下次文档修订校正分项计数与两处绝对化措辞。
- **是否需要修改 WP-30A：** 可选（P3 文档）。

---

## 未发现的问题类别（显式记录审查结论）

- **无跨上下文发布/状态污染**：语义清空 + 指纹 + 序号三重校验，全部拒绝路径有测试。
- **无可变静态全局状态、无跨视口共享**。
- **无伪造 Domain 数据**（伪 revision/CRS/bounds/几何）：类型系统 + 工厂固定注入 + 反射测试三重排除。
- **无字符串化降级**：typed HistoricalTime 全程同一实例（`Assert.Same` 证据）。
- **无绕过 Application 的持久化/Domain 查询路径**。
- **无未赋值的 rejectionReason 路径**（历史缺陷已被测试捕获并修复，当前代码核实）。
- **无 Thread.Sleep 时序依赖、无未界增长缓冲**（环形缓冲 64 封顶有测试）。
