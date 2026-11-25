# Project Session 关闭状态机与资源释放设计（WP-UI-02）

适用范围：PR #34 `feat/ui-02-window-lifecycle-master`。单机桌面程序，无并发会话请求；
Windows / macOS 一致；不为未发布旧实现保留兼容层。

## 1. 根因

1. `ProjectSession.CloseAsync` 用 `Closing` 作为“已在处理/已完成”的短路条件，异常留在
   `Closing` 后再次调用直接返回，导致剩余资源永远不释放。
2. 子工作区先从 `_workspaces` 移除再执行 `CloseAsync`，关闭失败后工作区丢失，无法重试。
3. `Closed` 状态与 `SessionClosed` 事件在资源释放异常时不会到达，但 UI 层无条件把
   `CloseAsync` 返回等同于成功并触发 `ProjectClosed`。
4. `ProjectSessionRegistry.CloseAllAsync` 在 `CloseAsync` 后无条件 `Remove`，关闭失败也会
   解除包路径占用，Registry 与真实会话状态脱节。
5. `StartWindow` 用 `_ = vm.LoadRecentProjectsAsync()` 丢弃 Task，存储读取异常成为未观察异常。

## 2. 最终状态机

| 当前状态 | 触发 | 结果 | 说明 |
| --- | --- | --- | --- |
| Ready / Opening / Validating / Migrating / Failed / Cancelled | `CloseAsync` | `Closing` | 开始释放；Opening 系列关闭后不再能 `TryCompleteOpen` 复活 |
| `Closing` | `CloseAsync` 再次调用 | 保持 `Closing`，直接返回 | 幂等：已有一次关闭正在执行，不重复释放 |
| `Closing` | 全部资源释放成功 | `Closed` | 只在此刻触发 `SessionClosed` 一次 |
| `Closing` | 任一资源释放抛异常 | `CloseFailed` | 记录 `CloseFailureDiagnostic` 并向上抛；不发事件、不解登记 |
| `CloseFailed` | `CloseAsync` / `DisposeAsync` 重试 | 回到 `Closing`，跳过已成功释放的资源 | 失败可恢复；不吞异常 |
| `Closed` | 任意关闭调用 | 保持 `Closed`，直接返回 | 幂等成功；不重复释放、不重复发事件 |

不引入锁框架或后台线程：仅沿用已有 `_stateGate` 保护状态/集合的短临界区，所有 `await`
在锁外执行。

## 3. 资源释放顺序与重试

固定顺序：**子工作区（后进先出）→ Asset Store → 数据库**。每个子工作区执行
`CloseAsync` 后再 `DisposeAsync`（释放其 `CancellationTokenSource`）。

- 子工作区：只有 `CloseAsync` + `DisposeAsync` 都成功才从 `_workspaces` 移除；失败保留在
  列表中。重试时 `CloseAsync` 幂等、`DisposeAsync` 幂等，不会重复取消或重复销毁 CTS。
- Asset Store / 数据库：各自用布尔标记记录“已成功释放”；仅未成功释放的资源会在重试时
  再次调用 `DisposeAsync`。前序资源成功、后序失败时，重试不会重复释放前序资源。
- `SessionClosed`：只在状态真正变为 `Closed` 时触发一次；`CloseFailed` 不触发。
- Registry：`SessionClosed` 订阅是唯一的成功解除登记路径；`CloseAllAsync` 仅在
  `session.State == Closed` 时 `Remove`，失败时保留占位并向上抛异常。
- 诊断：`CloseFailureDiagnostic` 记录最近一次失败的异常类型与消息，成功后清空；完整异常
  由调用链（ViewModel / 应用日志）呈现。

## 4. UI 层契约

- `ProjectWorkspaceViewModel.CloseProjectAsync`：`CloseAsync` 成功且 `Session.State == Closed`
  才触发一次 `ProjectClosed`；异常时写 `WorkspaceStatusText = "关闭项目失败：…"`，保留窗口。
- `ProjectWindow.Closing`：先取消本次关闭并执行项目关闭；只有 `ProjectClosed` 事件才允许
  窗口真正关闭。`_closeInProgress` 在 `finally` 复位，失败后仍可再次点击关闭。
- `StartWindow`：启动加载最近项目改为“观察 Task + 失败转状态 + 写应用日志”；存储异常不
  阻断启动，也不影响新建/手动打开。

## 5. 测试边界

- 单元测试：Session 状态机、释放顺序、失败注入与重试、`SessionClosed` 次数、Registry 占位、
  ViewModel 成功判定与状态文本、启动加载失败状态。
- 人工验收（Avalonia `Window` 生命周期在无头单元测试中不可靠，不写脆弱假测试）：
  重复 `ProjectOpened` 只保留一个工作区窗口；关闭成功后首页恢复；关闭失败窗口保留；
  关闭首页主窗口应用正常退出。需在 Windows / macOS 桌面环境按清单人工确认。
