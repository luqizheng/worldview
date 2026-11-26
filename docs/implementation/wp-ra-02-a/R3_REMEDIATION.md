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

---

# R3-R1 Remediation — 并发测试确定性修复（复审整改）

- 基线：R3 HEAD `a9915c6d6677e08733e8df2d1c5585ea1c418b9c`
- 复审发现：R3-R1-01（核心并发测试以"任务已创建"冒充"B 已到达锁竞争位置"）
  + R3-R1-02（R3 交付声称确定性信号，实际仍存在 10ms 轮询）。
- 生产代码 `PublishIfRequestCurrent` 的 `_stateGate` 方案保留不动。

## R3-R1-01：B 到达锁竞争位置的确定性证明

### 原缺陷

`switchBInitiated` TCS 在 `Task.Run(...)` 创建后立即触发，只能证明任务对象已创建，
不能证明 B 已实际执行到身份分配的锁竞争位置——正确实现与错误实现都可能因 B 尚未
运行而使判别断言碰巧通过。

### 修复方案：ContentionProbeResolver（测试自有，零生产代码改动）

关键依据是生产代码的**同步执行链**（零调度点）：

```
B 线程: workspace.SwitchContextAsync(branchC, …, ContentionProbeResolver)
  → await contextResolver.ResolveAsync(…)          // 返回已完成 Task + ConfigureAwait(false)
                                                    //   ⇒ 同线程内联继续，无线程跳转
  → lock (_gate) { … }                             // 会话锁，短暂、无竞争
  → ContextSwitched?.Invoke(Context)               // 同步多播委托（MapWorkspaceSession.cs:185）
      → VM 处理器同步前缀（_ = HandleContextSwitchedAsync 的首个 await 即
        RequestSceneAsync，其同步前缀直达 lock (_stateGate) { identity = ++ }）
```

`ContentionProbeResolver.ResolveAsync` 在 B 的线程上、返回已完成任务**之前**，
对 `_stateGate`（经反射读取私有字段，测试代码不变更生产可见性）执行
`Monitor.TryEnter(gate, TimeSpan.Zero)` 并立即退出——该点是 B 路径上距身份分配锁
最近的测试可达同步点，**探测点到锁之间零 await、零线程跳转**，探测发生即 B 已
实际推进到锁竞争位置。

判定语义（`LockFreeObserved`）：

- **false（锁被持有）⇒ 正确实现**：场景中唯一持锁者是 A 的发布回调（被测试阻塞在
  `PublishIfRequestCurrent` 临界区内，`publishEntered` 已确认）。B 随后真实执行的
  `Monitor.Enter` 必然阻塞——B 被**锁的所有权**钉在竞争位置。此时放行 A：读取
  `identityAtPublish`（仍在 A 的临界区内、动作返回之前）时，B 的分配需要先取得锁，
  而锁由 A 持有至动作返回 ⇒ **必为 2，与线程调度无关**。
- **true（锁空闲）⇒ 错误实现签名**：A 持锁的前提下不可能出现。测试随即等待 B 进入
  准备步（`GatedPreparation.WaitEnteredAsync(2)`——准备严格后于身份分配），使
  "B 的分配已完成"**先于放行被证明** ⇒ 放行后读取必为 3 ⇒ 核心断言
  `Assert.Equal(2, identityAtPublish)` **稳定失败**。

判定与断言之间**无任何时序假设**：两个分支的"分配 vs 读取"先后均由锁所有权或
TCS 信号（准备步进入严格后于分配）证明，而非时间窗口。

线程构造说明（任务书要求明确）：这是**跨线程锁竞争**，非同线程重入——A 的回调经
`Task.Run(() => dispatcher.DrainOne())` 在 drain 工作线程执行并持有 `_stateGate`；
B 经 `Task.Run` 在另一工作线程执行切换并在其线程上探测同一把锁。

### 破坏实验（错误实现稳定失败，未提交到 PR）

在临时 worktree `worldview-wt-r3r1-exp`（分支 `tmp/r3-r1-broken-impl-experiment`，
基于 R3 基线，实验后已删除）中，将 VM 发布路径改为"身份检查与 UI 赋值分离"
（R2 修复前形态：`LatestInitiatedRequestIdentity` 比较 + 直接赋值，无临界区），
其余代码不动：

| 运行 | 结果 |
|---|---|
| 1–5（连续 5 次） | 全部失败：`Assert.Equal() Failure: Expected: 2, Actual: 3` |

错误实现下探测必然返回"锁空闲"签名 → B 越过分配点 → 准备步进入信号证明分配先于
放行 → 读取见 3 → 核心断言稳定失败。实验代码未进入 PR 分支。

## R3-R1-02：TCS 信号替代轮询

- `FakeAppLog.WaitForInfoAsync(match, timeout)`：注册时先在锁内检查已有消息
  （消除"留痕先于注册"的丢失窗口），再注册 TCS 等待器；`Info` 触发首个匹配
  等待器。无轮询、无固定延迟；timeout 仅作挂死保护。
- `QueueingUiDispatcher.WaitPostedAsync(timeout)`：注册时队列已非空则立即完成，
  否则由下一次 `Post` 触发 TCS。
- R3 路径测试（全部 `R3P0_*` + R3 重写的 `R2P0_LateFailureAfterNewRequestPublished`）
  的 `WaitUntilAsync` 调用全部替换：
  - 投递等待 → `WaitPostedAsync`；
  - 丢弃留痕等待 → `WaitForInfoAsync`；
  - `PreparationFailures == 1` / `Superseded == 1` 计数轮询 → 删除（留痕在协调器
    Record 之后同一 continuation 内发生，留痕可见即计数已落定；计数断言保留在
    信号之后）；
  - Test C 的 `LatestInitiatedRequestIdentity == 3` 轮询 → 删除（非法上下文的
    失败路径为纯同步链：`SwitchContextAsync` 返回时身份已分配、回调已入队，
    直接断言）。
- `WaitUntilAsync`（有界条件轮询）保留，仅供 R1/R2 既有测试使用，注释已明确
  R3 路径禁止使用。
- 验证：对 R3 路径测试文本 grep `WaitUntilAsync|Task.Delay|Thread.Sleep|SpinWait`
  = 0 命中。

## R3-R1 验证记录

| 验证项 | 结果 |
|---|---|
| Desktop.Tests 全部 | 99/99 通过 |
| 核心并发测试连续 20 次 | 20/20 通过 |
| 破坏实验（错误实现）核心测试连续 5 次 | 5/5 稳定失败（Expected 2 / Actual 3） |
| Debug 全量 | 984/984 通过 |
| Release 全量 | 980/980 通过 |
| R3 路径轮询/固定延迟检查 | 0 命中 |
| Windows / macOS CI（Debug/Release） | 见 PR #40（推送后确认） |

## R3-R2：Test A 原子性测试重构（方案一：StateGateForTesting 观察入口）

### 背景

独立复审指出 R3-R1 遗留两个问题：

1. **暂停点过晚**：Test A 的暂停在 `PropertyChanged` 订阅者内，而属性赋值先更新
   backing field 再触发事件——订阅者接管时状态写入已完成，无法验证"身份判定 →
   状态写入"之间的原子性。
2. **探测手段被禁**：R3-R1 经反射 `GetField("_stateGate", NonPublic)` 获取真实锁，
   违反"不允许反射访问私有锁"的约束。

### 方案裁决（经用户与外部专家确认）

先实施纯黑盒方案并做破坏实验：错误实现下 **8 次运行 8 次全部漏检**。诊断测量
（以 B 到达 Resolver 为 0 时刻，8 次范围）：测试线程放行 A 为 26–138 µs、A 恢复并
读取身份为 51–453 µs、B 完成身份分配为 519–703 µs——**A 总是先读、B 总是后分配**，
漏检是系统性的而非概率性的。

根因：正确实现下 B 被锁阻塞后不再产生任何可观察事件（测试自有观察点全部位于分配
之后），"B 已被锁挡住"在纯黑盒下没有事件可等；放行 A 只能以"B 越过最后一个前置
同步点"为依据，而放行后 A 的读取与 B 的分配之间无同步关系。

经裁决采用**方案一**：`MapViewportBinding` 增加
`internal object StateGateForTesting => _stateGate;`（经 `InternalsVisibleTo` 供
测试程序集访问，与既有生命周期测试同一先例；仅暴露锁对象引用，生产代码一律使用
`_stateGate`，不经该入口操作锁；非公开 API）。该改动的动机是关键并发不变量的
确定性验证手段，不改变任何运行时行为。

### 测试拆分

原 Test A（`R3P0_PublishDuringCriticalSection_NewRequestIdentityExcluded`）拆分为：

| 测试 | 职责 |
|---|---|
| `R3P0_PublishActionHoldsIdentityGate_UntilActionCompletes` | Binding 底层原子性：直接驱动 `PublishIfRequestCurrent`，暂停点在 publish Action **内部**；经 `GateProbeResolver` 探测真实锁。不经过 UI Dispatcher。 |
| `R3P0_ViewModelPublishesContextStatusThroughBindingGuard` | ViewModel UI 发布接线：ContextSwitched 触发重请求、Dispatcher 投递与执行线程、成功/失败状态正常发布、旧请求不得覆盖新请求（投递前丢弃 + 回调内守卫拒绝）。不承担底层锁原子性证明。 |

### 底层测试的同步时序（双分支确定性）

1. 线程 A 调用 `PublishIfRequestCurrent(identityA, action)`，在 action 内部置位
   `publishEntered` 并暂停（等待 `releasePublish` TCS）。正确实现此刻持有
   `_stateGate`。
2. 线程 B 经真实链路发起请求；`GateProbeResolver.ResolveAsync`（B 的线程）对
   `StateGateForTesting` 执行 `Monitor.TryEnter(TimeSpan.Zero)`，结果经
   `LockFreeObserved` TCS 交给测试线程，**探测先于信号**；随后照常返回，B 继续
   沿真实链路推进到身份分配。
3. 测试线程等待 `LockFreeObserved` 后分派：
   - **false（锁被持有）** ⇒ 本场景唯一持锁者即 A 的发布临界区 ⇒ 正确实现；
     B 的真实 `Monitor.Enter` 必然阻塞（B 确定性钉在竞争位置）。放行 A →
     A 在 Action 内（仍持锁）读取最新身份必为自身身份 → A 返回释放锁 →
     B 取得 identityA+1 并完成发布（锁释放的 happens-before）。
   - **true（锁空闲）** ⇒ A 在 Action 内（publishEntered 已证明）而锁空闲 ⇒
     判定与写入脱离同一临界区（错误实现签名）⇒ 断言**立即稳定失败**
     （`Assert.False(lockFree, "身份门锁空闲…")`），无调度窗口。
4. 场景中锁只有两个持有者（A 的发布临界区、B 即将进入的分配临界区），B 自身在
   探测时刻位于分配锁之前、不可能持锁——判定无歧义。TryEnter 成功时在解析器
   finally 中释放，不遗留锁状态。

### 破坏实验（临时 worktree，未入库）

实验分支 `tmp/r3-r2b-broken-impl`（自 `bbf52a6` 创建，实验后连同 worktree 一并
删除），对 `PublishIfRequestCurrent` 移除锁保护（判定与 `publish()` 分离），运行
**同一个**底层测试：

| 实现 | 结果 |
|---|---|
| 错误实现（无锁）连续 5 次 | **5/5 稳定失败**，失败信息均为"身份门锁空闲：身份判定与状态写入未受同一锁保护" |
| 正确实现连续 20 次 | **20/20 全部通过** |
| 恢复生产实现后全量 | Debug 985/985、Release 981/981 通过（Test A 拆分净增 1 个测试） |

对照：纯黑盒方案在错误实现下 8/8 漏检——真实锁探测的检出能力是决定性的。

### 生产代码改动边界

- `MapViewportBinding.cs`：**仅**新增 `StateGateForTesting` 属性（8 行含文档），
  `PublishIfRequestCurrent` 与锁语义零改动。
- `ProjectWorkspaceViewModel.cs`：零改动。

### R3-R2 验证记录

| 验证项 | 结果 |
|---|---|
| Desktop.Tests 全部 | 100/100 通过 |
| 底层原子性测试连续 20 次（正确实现） | 20/20 通过 |
| 底层原子性测试连续 5 次（错误实现，临时 worktree） | 5/5 稳定失败 |
| Debug 全量 | 985/985 通过 |
| Release 全量 | 981/981 通过 |
| R3 路径轮询/固定延迟/反射检查 | 0 命中（`WaitUntilAsync` 仅 R1/R2 既有测试使用） |
| Windows / macOS CI（Debug/Release） | 见 PR #40（推送后确认） |
