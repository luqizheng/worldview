# WP-RA-02-A R3 Remediation — 请求状态发布原子性与确定性测试修复

- 基线：R2 HEAD `7541e27a9261d7e8af78b5efa96c038f035f4c00`
- 审查发现：R3-01（ViewModel 身份检查与 UI 状态写入之间的 check-then-write 竞态）
  + R3-02（回归测试依赖固定 `Task.Delay(100)`）
- 本文件为新增记录，不修改 R1/R2_REMEDIATION.md 及任何既有历史文档。

## 1. R3-01：ViewModel 身份检查与 UI 状态写入之间的竞态

### 原始竞态时序（R2 实现残留）

R2 的 `PublishContextSwitchStatus` 在 Dispatcher 回调内：

1. 回调执行时检查 `IsRequestCurrent(...)`（读 `LatestInitiatedRequestIdentity`）；
2. 检查通过后执行 `WorkspaceStatusText = ...`。

两步之间**无同步保护**。可能时序：

- 请求 A 的 Dispatcher 回调开始执行，身份检查通过（A = 最新）；
- 另一线程发起请求 B，在 Binding 的 `_stateGate` 内 `++_latestInitiatedRequestIdentity`
  （B 取得更新身份）；
- A 继续执行属性赋值——旧请求 A 的状态覆盖写入 UI。

这与 R2 §5 已修复的 Binding 内部 check-then-write 竞态同构：检查与写入脱离同一
临界区，新请求可插入两步之间。

### 修复方案：Binding 同步发布原语 `PublishIfRequestCurrent`

新增 `MapViewportBinding.PublishIfRequestCurrent(long requestIdentity, Action publish)`：

```csharp
internal bool PublishIfRequestCurrent(long requestIdentity, Action publish)
{
    lock (_stateGate)
    {
        if (_isClosed || _latestInitiatedRequestIdentity != requestIdentity)
        {
            return false;
        }

        publish();
        return true;
    }
}
```

**同步关系**：`_stateGate` 同时串行化两类操作——

1. `RequestSceneAsync` 的请求身份分配（`lock (_stateGate) { identity = ++_latest...; }`）；
2. 本方法的"身份判定 + UI 状态发布"。

**为什么窗口不存在（线性化论证）**：设 A 的发布与 B 的身份分配为两个临界区操作，
二者竞争同一把锁，执行顺序只有两种且互斥：

- **B 先取得锁**：B 的身份分配完成（`_latest` = B）→ A 的发布随后进入临界区时
  判定 `identity_A != _latest` → 拒绝发布（返回 false，留痕）；
- **A 先取得锁**：A 的判定通过**且完整执行** `publish()`（属性赋值、PropertyChanged
  同步回调全部走完）才释放锁 → B 的身份分配因果上晚于 A 的发布完成，A 的发布
  线性化于"仍为最新"的时刻，其后 B 的发布覆盖——最终状态归属最新有效请求。

"检查通过 → 被抢占 → 旧请求继续写入"的交错在锁内**不可构造**。

**VM 接入（三层守卫结构）**：

1. **投递前预检**（`IsRequestCurrent`，优化层）：注定过期的请求不入队，丢弃留痕；
2. **回调内关闭检查**（终态 sticky 标志）：Binding/Workspace 关闭后不再修改状态；
3. **`PublishIfRequestCurrent`**（正确性层）：身份判定与 `WorkspaceStatusText` 赋值
   在 `_stateGate` 内原子完成。UI 属性赋值仍只发生在 UI Dispatcher 线程（回调内
   同步执行，锁内无 await）。

### 锁、线程与重入分析

- `_stateGate` 为 Binding **实例私有** Monitor 锁（非全局、非框架），无嵌套获取
  （不与 VM 的生命周期锁形成锁顺序对），无死锁环；
- 锁内无 `await`、无 I/O；持有时间 = 一次身份比较 + 一次属性赋值；
- **重入（约束 8）**：`publish()` 触发的 `PropertyChanged` 同步订阅者在临界区内执行：
  - 同线程重入 `_stateGate`：Monitor 可重入，不会自锁；
  - 订阅者同步发起新请求（重入身份分配）：其发生因果上晚于本次已通过的身份判定
    （判定先于 publish 执行），新请求自身的发布随后覆盖——最终状态仍归属最新请求，
    无状态倒退（R3 竞态测试 `R3P0_PublishDuringCriticalSection_NewRequestIdentityExcluded`
    确定性验证该不变量）；
  - 订阅者抛异常：Monitor 语义下异常即退出临界区并释放锁，锁不留存损坏状态；
    VM 的 catch 就地记录（Warn），异常不外泄到 Dispatcher。
- **关闭状态（约束 5）**：`_isClosed` 为 sticky 终态，锁内一并判定；回调前的关闭
  检查与锁内判定双保险，关闭后任何发布拒绝。

## 2. R3-02：固定延迟测试改为确定性同步

`R2P0_LateFailureAfterNewRequestPublished_IsDiscardedBeforeDispatch` 原以
`await Task.Delay(100)` 等待"过期失败的发布判定走完"——固定延迟既不能证明目标
异步操作完成，也掩盖了测试时序的语义错误（见下）。

### 测试时序的语义修正（顺带发现）

追踪诊断发现，基线实现下该测试**名不符实**：`GatedPreparation` 的 `Throw` 检查在
`Gate` 等待**之前**，脚本 `(Gate: true, Throw: true)` 实际是"立即抛出"而非"门控挂起
后抛出"。因此 A 的失败在 A 仍为最新请求时即落定并入队（投递前守卫放行），真正的
丢弃发生在**回调执行时身份守卫**；基线断言 `PostedCount == 0` 之所以通过，只是因为
`Drain` 已清空队列——测试实际验证的是回调时守卫，而非其名称声称的投递前丢弃。

修正两处：

1. `GatedPreparation.PrepareAsync` 调整为**先等门、再抛出**（`(Gate: true, Throw: true)`
   语义与全部三处调用点注释的意图一致；`(Gate: false, Throw: true)` 立即失败行为不变）；
2. 测试时序变为真实"晚到失败"：A 门控挂起 → B 成功发布 → 释放门 → A 失败落定
   （身份 2 已过期）→ 投递前守卫丢弃。

### 确定性完成信号：丢弃留痕

生产代码的丢弃路径统一经 `LogDiscardedStatus` 以 Info 级留痕（消息含
`已丢弃（投递前守卫）` / `已丢弃（回调执行时身份守卫）`）。判定与留痕之间**无任何
await/调度点**，故"留痕可见 ⇔ 身份判定已执行且结果为丢弃"。测试以该留痕为完成
信号（`WaitUntilAsync` 有界轮询，条件驱动、非固定等待），信号出现后再断言
Dispatcher 队列与 UI 状态。`Task.Delay(100)` 已删除；本文件内仅存
`WaitUntilAsync` 的 10ms 轮询步进（R1 起既有，等待的是"门控已确定的离散状态"的
传播，带超时上限，非固定延迟）。

### 协调器计数不作为晚到判定信号的原因（Test C 修正）

Test C（旧请求晚到成功）原等待 `Coordinator.Counters.Superseded == 1`，超时。根因：
B 因非法上下文在请求创建阶段失败，**从未注册进协调器**——协调器无从知晓身份 3，
不会把 A 标记为 Superseded（A 在协调器侧正常 Published）。过期判定的权威是
**Binding 的最新发起身份**（R3-01 正确性层），故该测试同样以投递前守卫的丢弃留痕
为完成信号，并断言 `LatestInitiatedRequestIdentity == 3`。

## 3. 新增/调整测试覆盖

| 测试 | 场景 | 验证行为 |
|---|---|---|
| `R2P0_LateFailureAfterNewRequestPublished_IsDiscardedBeforeDispatch`（R3-02 重构） | Test E：A 门控挂起 → B 成功发布 → A 晚到失败 | 投递前守卫丢弃（留痕信号），PostedCount==0，B 状态不被覆盖，PreparationFailures==1 |
| `R3P0_PublishDuringCriticalSection_NewRequestIdentityExcluded` | Test A：A 的发布进入 `_stateGate` 临界区（经 PropertyChanged 订阅者门控保持临界区打开）时，从临界区内发起 B | B 的身份分配被排除在 A 的"判定→写入"之外（identityAtPublish==2）；A 先完整发布，B 随后覆盖——最终状态归属最新请求 |
| `R3P0_LatestRequestSuccess_StaleLateCompletionCannotOverwrite` | Test B：最新请求成功发布后，旧请求晚到完成 | 成功状态保留，旧请求不得覆盖 |
| `R3P0_LatestRequestFailure_IsPublished_StaleLateSuccessCannotOverwrite` | Test C：最新请求（非法上下文）失败正常发布，旧请求晚到成功 | 失败如实发布（不为过滤过期而误丢当前失败）；晚到成功被投递前守卫丢弃（留痕信号），状态不倒退 |
| `R2P0_QueuedFailureCallbackAfterWorkspaceClose_IsDiscarded`（R2 既有） | Test D（VM 级）：关闭工作区后执行已入队的失败回调 | 回调执行时关闭守卫丢弃，关闭后状态不被修改 |
| `R3P0_PublishIfRequestCurrent_ClosedBinding_IsRejected` | Test D（Binding 原语级）：Binding 关闭后 `PublishIfRequestCurrent` | 返回 false，publish 不执行 |

同步手段：TCS 门控的 `GatedPreparation`、门控/工作线程解析替身、队列式 UI 调度器
（`DrainOne` 精确控制回调执行时刻）、PropertyChanged 订阅者门控（Test A 的临界区
保持）。无固定延迟、无调度碰运气；轮询仅等待门控确定的离散状态，带超时上限。

`FakeAppLog` 扩展记录 Info 级消息（生产代码将"过期结果丢弃"定为正常诊断事件，
Info 语义恰当；测试观察其作为完成信号），并新增 `InfoCount` / `InfoMessages`。

## 4. 验证记录（真实执行结果）

| 验证项 | 命令 | 结果 |
|---|---|---|
| Desktop 测试（Debug） | `dotnet test tests/WorldView.Desktop.Tests/...csproj` | 99/99 通过 |
| 竞态测试连续 20 次 | `--filter FullyQualifiedName~R3P0_PublishDuringCriticalSection|~R2P0_LateFailure|~R3P0_LatestRequestFailure` × 20 | 20/20 全部通过（每次 3/3） |
| 固定延迟检查 | `grep "Task.Delay\|Thread.Sleep"` 本测试文件 | 仅存 `WaitUntilAsync` 10ms 轮询步进（既有，条件驱动）；`Task.Delay(100)` 已删除 |
| Debug 全量 | `dotnet test`（解决方案） | 984/984 通过 |
| Release 全量 | `dotnet test -c Release` | 980/980 通过（Debug 多出的 4 个为 `#if DEBUG` 包裹的 `DebugCloseFaultTests`，符合预期） |
| Windows CI（Debug/Release） | 推送后 GitHub Actions | 见 PR #40 状态（推送后确认） |
| macOS CI（Debug/Release） | 推送后 GitHub Actions | 见 PR #40 状态（推送后确认） |

环境说明：测试执行前置 `TEMP`/`TMP` 指向工作区内目录（agent 沙箱子进程无法写
系统临时目录的既有环境约束，与被测代码无关）。

## 5. 语义不变式汇总

1. 请求身份分配与 UI 状态发布共用 `_stateGate` 临界区，二者对同一 Binding 实例
   完全串行化——check-then-write 窗口不存在；
2. 最新请求的成功与失败都如实发布（失败不因过期过滤而误丢）；
3. 过期请求的成败结果一律丢弃并留痕，不覆盖、不静默；
4. 关闭（Binding/Workspace 终态）后不接受任何发布；
5. UI 属性赋值只发生在 UI Dispatcher 线程；锁内无 await、无 I/O、无嵌套锁。
