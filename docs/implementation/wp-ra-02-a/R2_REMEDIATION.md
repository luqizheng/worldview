# WP-RA-02-A R2 Remediation — Context Switch 失败路径的请求乱序修复

- 基线：R1 HEAD `702b2626f01a6d08a7946e67b75bf191f9eeec98`
- 审查发现：R2-P0（失败路径缺少 RequestIdentity 保护）+ §5（请求状态记录 check-then-write 竞态）
- 本文件为新增记录，不修改 R1_REMEDIATION.md 与任何既有历史文档。

## 1. R2-P0：失败路径请求身份保护

### 原始竞态时序

R1 中成功路径（`PublishContextSwitchStatus`）已有投递前 + 回调执行时双重 `IsRequestCurrent` 守卫；
但失败路径 `PublishContextSwitchFailureStatus` 只检查 Binding/Workspace 是否关闭：

1. 请求 A（语义切换）在请求创建阶段抛异常（如上下文校验失败）；
2. A 的异常在 `HandleContextSwitchedAsync` 的 catch 中被观察，但 **A 的请求身份丢失**
   （异常对象不携带身份，catch 也未取得）；
3. A 的失败状态经 `PublishContextSwitchFailureStatus` 入队（仅关闭守卫放行）；
4. 请求 B 发起并成功发布（B 为最新请求）；
5. Dispatcher 执行 A 的失败回调——旧路径只查关闭状态 → **A 的失败覆盖 B 的成功状态**。

### 修复方案：失败归一化 + 统一发布路径

选择任务书授权的"调整请求结果封装方式"路线（而非异常包装），理由：

- 协调器自身契约即把准备阶段异常归一化为 `Failed` 结果（`PresentationRequestCoordinator
  .PrepareAndPublishAsync` 的 catch 链），Binding 层的归一化与该既有架构一致；
- 成功与失败共用 **同一个发布方法 + 同一套 `IsRequestCurrent` 判定**，满足
  "复用统一有效性判断、避免两套规则漂移"的要求；不存在第二套守卫逻辑。

实现（`MapViewportBinding.RequestSceneAsync`）：

1. 请求身份在发起时分配（`_stateGate` 锁内 `++`），**贯穿完整生命周期**——
   `SceneRequestResult` 无论成败都携带 `RequestIdentity`；
2. 管线异常（请求创建校验失败 / 协调器契约外漏出的异常）在本方法内归一化为
   `State=Failed` 的 `PresentationRequestOutcome`（Sequence=0，与协调器
   `RejectedClosedOutcome(sequence: 0)` 约定一致），**异常对象原样放入
   `SceneRequestResult.Failure`** 由调用方观察记录——不吞异常、不以"catch 时读取
   全局最新身份"伪造（那可能已是后续请求的身份）；
3. `HandleContextSwitchedAsync`：`request.Failure` 非 null 时先记录日志（异常观察），
   随后与成功结果一样走 `PublishContextSwitchStatus`（投递前身份检查 + 回调执行时
   身份检查）；`PublishContextSwitchFailureStatus` 已删除；
4. VM 的 catch 降为纯防御路径：管线之外无身份可用的异常仅记录、**不发布状态**
   （无身份可判定有效性，发布即可能让旧失败覆盖当前请求状态）；
5. Open 路径同样观察 `initialRequest.Failure` 并经 `DescribeOutcome` 如实显示失败。

## 2. §5：请求状态记录竞态

`RequestSceneAsync` 原实现：

```csharp
if (Volatile.Read(ref _latestInitiatedRequestIdentity) == identity)
{
    LastRequestedFingerprintHex = ...;   // 或 Volatile.Write(ref _lastOutcome, outcome);
}
```

**竞态确认存在**：check-then-write 非原子。A 检查通过后、写入前，请求 B 发起
（`Interlocked.Increment`），A 仍将过期结果写入 `_lastOutcome` / 指纹——状态记录倒退。
窗口极窄但真实（A 的完成与 B 的发起在不同线程无序交错即触发）。

**修复**：新增每实例 `_stateGate` 局部锁（单机桌面定位的最小同步方案，锁内无 await）：

- 发起：身份分配与指纹记录原子；
- 完成：身份判定与 `_lastOutcome` 写入原子（`RecordOutcomeIfCurrent`）；
- 读者仍无锁（`Volatile.Read`），锁退出时的 full fence 保证可见性。

正确性论证：写入者全部串行于 `_stateGate`。若 A 的判定通过（判定时刻 B 未发起），
则 B 的发起与判定-写入必然晚于 A 的锁段 → 最终状态归属 B；若 B 先发起，A 判定失败
不写入。任何交错下最终记录都是"当时最新请求"的结果，不存在旧请求后写覆盖。

未为该微窗口增加专项测试：竞态窗口为纳秒级、不可确定性构造；修复为结构性原子化
（锁消除窗口而非缩小窗口），由既有全部身份测试（A-xx + R1 + R2）在锁保护下持续通过
作回归背书。

## 3. 回归测试（确定性，无随机延迟）

全部位于 `DesktopPresentationR1RemediationTests.cs`（R2 节），竞态时序由
TCS 门控准备函数、非法上下文与队列式 UI 调度器（新增 `DrainOne` 精确控制回调执行
时刻）确定性构造：

| 测试 | 规格 | 时序与断言 |
|---|---|---|
| `R2P0_LateFailureAfterNewRequestPublished_IsDiscardedBeforeDispatch` | Test 1 | A 门控挂起 → B 发布 → A 晚到失败 → 投递前身份守卫丢弃（失败回调不入队 `PostedCount==0`）；状态/结果不被覆盖；失败经协调器计数可观察 |
| `R2P0_QueuedFailureCallbackExecutesAfterNewRequestStarted_IsDiscardedInCallback` | Test 2 | A 失败回调入队（A 当期为最新）→ B 启动（门控）→ `DrainOne` 执行 A 回调（身份 2≠3 → 丢弃）→ 释放 B → B 成功发布。**回归验证：该测试在 R1 代码上失败（Actual="语义切换重请求失败：…"）** |
| `R2P0_CurrentRequestFailure_IsPublished` | Test 3 | 由更新的 `R1P1_01_RequestSceneException_ExceptionObservedLoggedAndVisualized` 承担：最新请求失败 → 守卫放行 → 失败状态可见 + 异常日志观察 + Failed 结果入 `LastRequestOutcome` |
| `R2P0_QueuedFailureCallbackAfterWorkspaceClose_IsDiscarded` | Test 3 附注 | 失败回调入队后关闭工作区 → `DrainOne` 执行回调 → 关闭守卫丢弃，状态保持"已关闭" |

规格 Test 1 的字面时序说明：物理上"晚抛出的异常"在当前协调器契约下不存在（准备异常
均被归一化为 Failed 结果；Binding 归一化后管线异常也不再抛出）。异常路径失败的唯一真实
迟到机制是 **Dispatcher 队列滞后**（投递时 A 最新、回调执行时 B 已启动）——即 Test 2 的
构造；Test 1 覆盖其互补分支（失败结果晚到、投递前即被丢弃）。两层守卫合计完整覆盖
规格要求的"投递前检查 + 回调执行时检查"。

## 4. 语义变更说明

- 异常路径失败的状态文本由 `语义切换重请求失败：{message}` 变为统一格式
  `工作区「X」语义切换：场景请求失败：{diagnostic}`（与成功路径同源 `DescribeOutcome`）；
  对应 R1 测试断言已同步更新（测试意图不变：失败可观察、日志记录、不冒充成功）。
- 归一化后，请求创建失败的 Failed 结果会以自身身份进入 `LastRequestOutcome`
  （旧实现异常路径不触碰 LastRequestOutcome）——失败可观察性增强，符合
  "失败不冒充成功"契约。
- 未改变：Coordinator latest-request-wins、C1/C2、F-1、CaptureFrame 唯一捕获点、
  IUiDispatcher 边界、Application 无 Avalonia 依赖。

## 5. 验证

- Desktop.Tests：93/93 × 4（含 3 连续稳定性运行）；
- 全解决方案 Debug 978/978；Release 974/974（差 4 = `DebugCloseFaultTests` `#if DEBUG`，既有差异）；
- 四矩阵 CI 结果见 PR Checks。
