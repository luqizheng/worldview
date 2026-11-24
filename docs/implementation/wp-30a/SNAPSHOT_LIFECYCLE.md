# WP-30A 快照与生命周期语义

## 1. 不可变所有权（immutable ownership）

| 类型 | 所有权机制 |
|---|---|
| `SceneSnapshot` | 图层集合 `ImmutableArray<SceneLayer>` 私有持有；经 `ICollection<T>`/`IList<T>` 转型写回抛 `NotSupportedException`（测试证明"真正不可变"，非接口性只读） |
| `SceneLayer` / `SceneLayerDescriptor` | 场景项 `ImmutableArray<SceneItem>` 私有持有（同上证明） |
| `PresentationContext` | 全部字段为不可变值/不可变 Domain 类型；构造期固定指纹与语义身份 |
| `PresentationResult<T>` | 构造后全部只读；Completed 必须携带非空载荷，Failed 必须携带诊断 |

- 构建后篡改源集合（`List<T>`）不能影响已产出结果（所有权转移测试锁定）。
- 快照不含：Avalonia/Skia 后端资源、数据库句柄、可变 Domain 实体、ViewModel、会话、
  事件订阅、闭包可变状态、无主缓冲区（PRA-01 §4.2 禁令清单）。
- 后端资源（GPU/Skia 句柄）**不进入**逻辑快照；其所有权/释放/重建归 WP-30B 渲染管线。
  M1 空白快照无此类资源，但生命周期契约已按后续资源边界建立。

## 2. 原子发布与读者（atomic publish & readers）

- 发布路径单写者（协调器持发布决定权）+ `Interlocked.Exchange` 原子交换；
  读者 `Current` = `Volatile.Read`，**无锁、无等待、无准备工作**（PRA-01 §6.3/§6.5）。
- 读者契约：一次捕获引用，整个读取/绘制过程复用同一引用；发布者后续发布不改变
  已捕获引用（测试锁定）；渲染回调绝不等待更新快照。
- 发布六步校验（PRA-01 §6.2，发布器 `TryPublish`）：
  1. 发布器未释放（`publisher_disposed`）；
  2. 候选非 null（`candidate_missing`）；
  3. 候选指纹 == 期望指纹（`context_mismatch`）；
  4. 请求序号 == 活动请求序号（`stale_request`）；
  5. 候选版本 > 已发布版本（`non_monotonic_version`）；
  6. 全部通过才原子交换；任一失败整体拒绝并保留当前快照（拒绝原因码可查）。

## 3. 快照版本（SnapshotVersion）

- 由**发布器**分配（`AllocateCandidateVersion`，Interlocked 单调递增），不由 worker 分配。
- 发布器生命周期内严格递增；被拒绝的候选允许缺口，绝不复用已发布版本。
- 版本校验顺序：指纹 → 序号 → 版本（顺序有测试锁定；构造对应拒绝场景时需满足
  前置条件，见 `SceneSnapshotPublisherTests` 注释）。

## 4. 请求生命周期（PRA-01 §7.1 状态机）

```
Created → Preparing → Assembling → Ready → Published
                     ↘ Cancelled（协作取消生效；仅省功，非正确性机制）
                     ↘ Superseded（被更新请求取代；晚完成一律拒绝）
                     ↘ Failed（准备失败/装配拒绝；装配拒绝带 RejectionStage）
                     ↘ RejectedStale（发布校验拒绝）
                     ↘ RejectedClosed（关闭/停机后到达；本实现对契约状态的显式扩展）
```

- 状态为终态后不可逆；`PresentationRequest.State` 供诊断读取。
- 每请求结果落定后写入**有界环形缓冲**（容量 64，`RecentOutcomes` 旧→新副本），
  含序号/指纹/MapId/BranchId/时间文本/终态/诊断/发布版本/落定时刻——不记录任何
  地图内容（PRA-01 §9 隐私边界）。计数器：requested/rejected-closed/prepared/
  published/superseded/cancelled/preparation-failures/assembly-rejections/
  publication-rejections。

## 5. Latest-request-wins（正确性三重身份）

正确性由**请求身份 + 上下文指纹 + 生命周期状态**共同保证；取消只用于省功：

1. 新请求激活时立即取代旧的未发布请求（旧请求晚完成一律 `Superseded`，即使其
   准备函数忽略取消令牌并产出完整结果——测试 E9 锁定）；
2. 发布前校验候选指纹等于当前期望指纹（语义切换后的迟到候选不可发布）；
3. 协调器关闭/发布器释放后的任何完成路径一律 `RejectedClosed`，不可发布。

关键串行化点：请求激活（supersede + 语义清空判定）与完成门（closed/superseded 判定）
在协调器 gate 内串行；发布校验在发布器 gate 内串行。锁序 coordinator-gate → publisher-gate
单方向，无死锁。

## 6. Last-known-good（M1 presentation policy）

| 场景 | 行为 |
|---|---|
| 首次构建 | `Current` = null（读者可见 null → WP-30B 显示加载态），成功后发布 v1 |
| 同语义刷新（视口代变化）准备中 | 保留当前快照（last-known-good） |
| 同语义刷新失败 | 保留当前快照；结果记 `Failed` |
| 语义切换（Map/Branch/RootHistory/时间变化） | 激活时**原子清空** `Current`（读者立即看到 null，旧上下文不冒充新语义）；`ContextSwitchClearCount` 递增 |
| 语义切换后新请求失败 | 保持 null（不复活旧上下文） |
| 语义切换后旧请求晚完成 | `Superseded`，不可发布 |
| 语义切回旧 Map | 再次清空判定（无快照可清），新请求成功后恢复该语义场景 |
| 关闭/停机 | 不清空已发布快照（逻辑数据在被引用期间保持有效，PRA-01 §8）；但一切新发布被拒绝 |

语义切换立即清空是本实现的显式 M1 presentation policy 决策（Profile §6.4 允许
"clear or replace" 二选一）：单一权威、可测试、避免任何旧上下文误显窗口。

## 7. Close / Shutdown / Dispose

- `PresentationRequestCoordinator.CloseAsync()`：幂等；停止接受新请求；协作取消活动请求；
  其后的完成与新请求一律 `RejectedClosed`。
- `SceneSnapshotPublisher.Dispose()`：拒绝一切后续发布与版本分配；**不清空** `Current`
  （已发布快照在被引用期间保持有效）。
- `ViewportPresentationRuntime.DisposeAsync()`：先关协调器、再释放发布器（顺序即契约）；
  幂等。关闭后：新请求拒绝、迟到完成拒绝、直接 `TryPublish` 拒绝（测试 D/E 类锁定）。

## 8. 视口隔离

每个视口一个 `ViewportPresentationRuntime` 实例（独立序号空间、独立版本计数、独立
语义身份、独立诊断）。视口 A 的请求不取代、不覆盖、不清空视口 B（测试锁定）。
无任何全局/静态协调状态（AGENTS.md §E 纪律）。

## 9. M1 已知边界（为 WP-30B 建立的契约）

- 准备函数（`ScenePreparation` 委托）可注入——WP-30B 的真实场景准备（将来含 World
  State 依赖内容时须声明 NotEvaluated 拒绝语义）在此插槽接入；M1 默认实现即空白场景组合。
- 读者（渲染 draw 回调）尚未存在；读者契约由测试替身验证（PRA-01 §10.4 允许），
  真实控件验证属 WP-30B/PRA-02。
- 诊断仅为进程内计数器 + 有界环形缓冲；无持久化、无跨进程分发。
