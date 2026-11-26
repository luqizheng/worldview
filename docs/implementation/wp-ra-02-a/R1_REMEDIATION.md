# WP-RA-02-A R1 Remediation — Desktop Presentation Pipeline Integration

> 状态：待独立复审（PR #40 保持 Draft）
> 基线：`e1c75200340a4e92f7e31bc26c2571bc73b4cdc5`（A-01～A-10 审查基线）
> 本文档为 WP-RA-02-A R1 修复的设计依据与实现记录；不修改任何既有历史文档。

## 1. R1-P1-01 — ContextSwitched 异步线程边界

### 问题

`MapWorkspaceSession.SwitchContextAsync` 的异步解析路径使用 `ConfigureAwait(false)`，
真实异步完成的 Context 切换会在 ThreadPool 线程触发 `ContextSwitched`。
ViewModel 原处理逻辑的 `ConfigureAwait(true)` 只捕获调用时的 SynchronizationContext，
事件源线程无 Avalonia 同步上下文时，`WorkspaceStatusText` 的更新存在跨线程风险。

### 修复

- 新增 Desktop 表现层 UI Dispatcher 边界：`WorldView.Desktop/Ui/IUiDispatcher.cs`
  - `IUiDispatcher`（`CheckAccess` / `Post(Action)`）；
  - `AvaloniaUiDispatcher`（生产实现，`Dispatcher.UIThread.Post`，组合根 Program.cs 注入）；
  - `InlineUiDispatcher`（测试宿主专用：无 Avalonia 消息循环的环境下内联执行，
    保持既有测试确定性；投递边界的正确性由受控队列调度器专项测试验证）。
- `ProjectWorkspaceViewModel` 构造注入 `IUiDispatcher`（默认 `AvaloniaUiDispatcher`，
  缺省即安全）；`HandleContextSwitchedAsync` 改为 `ConfigureAwait(false)` + 显式投递：
  - 事件处理器只做线程无关的判定与请求发起（Application/Presentation 调用线程安全）；
  - 一切 ViewModel 属性更新经 `_uiDispatcher.Post` 投递到 UI 线程；
  - 投递回调执行时双重守卫（见 R1-P2-01 身份 + 关闭检查）：投递延迟期间工作区
    已关闭或请求已过期则跳过；
  - 回调内异常就地捕获并记录（`IAppLog.Warn`），不留不可观察的 Dispatcher 未处理异常；
  - 重请求抛出真实异常时：触发线程记录日志 + 失败状态经 Dispatcher 可视化。
- **未改动** `MapWorkspaceSession` / Application / Domain（保持 `ConfigureAwait(false)`，
  不引入 Avalonia 依赖；按审查要求未以删除 `ConfigureAwait(false)` 方式"修复"）。

### 测试

`DesktopPresentationR1RemediationTests`：

- `R1P1_01_SwitchCompletedOnWorkerThread_StatusUpdateMarshalsThroughUiDispatcher`：
  真异步 Resolver（ThreadPool 工作项完成，非 `Task.FromResult`）→ 状态更新仅在
  受控 Dispatcher Drain 后落地，执行线程为调度器线程，不随事件源线程执行。
- `R1P1_01_QueuedUpdateAfterWorkspaceClose_IsSkippedOnDispatch`：投递已入队后
  关闭工作区，Drain 时投递回调守卫跳过，已关闭工作区状态不被修改。
- `R1P1_01_RequestSceneException_ExceptionObservedLoggedAndVisualized`：
  请求创建阶段真实异常 → 日志记录 + 失败状态可视化，无未观察异常。

## 2. R1-P2-01 — 请求完成乱序导致状态倒退

### 问题

多个场景请求异步执行并以不同顺序完成；旧请求完成仍无条件写入
`MapViewportBinding.LastRequestOutcome` 与 `WorkspaceStatusText`，可能使状态倒退。

### 修复

- `MapViewportBinding` 引入**每实例**请求身份：
  - `RequestSceneAsync` 发起即 `Interlocked.Increment` 分配私有单调身份（非全局
    静态，Workspace 之间互不影响；关闭再重新打开的工作区持有全新实例，身份天然隔离）；
  - 完成时仅当身份仍为最新才写入 `LastRequestOutcome` 与 `LastRequestedFingerprintHex`
    ——过期请求（Superseded / 迟到失败 / 关闭后迟到完成）不得覆盖当前请求的结果记录；
  - 返回值改为 `SceneRequestResult(Outcome, RequestIdentity)`（internal record），
    调用方据此执行相同的有效请求判定。
- `ProjectWorkspaceViewModel`：
  - `HandleContextSwitchedAsync` / OpenWorkspaceAsync 路径以同一身份规则守卫
    状态文本更新；过期请求静默让位（新请求的处理器会发布自己的结果）；
  - 语义切换路径的投递守卫 = 身份有效 + Binding/工作区存活（双重，投递前后各一次）；
  - OpenWorkspaceAsync 路径的守卫只看身份：本请求自身的 RejectedClosed 等
    结果即便发生在关闭之后也如实显示（失败不冒充成功，A-05 语义保持）。
- **未改动** Coordinator 的 latest-request-wins 发布语义（三重身份机制不变；
  本修复只约束表现层"最近结果记录与状态文本"的有效请求判定）。

### 测试

- `R1P2_01_LateSupersededCompletion_DoesNotRegressOutcomeOrStatus`：请求 2 门控挂起、
  请求 3 发起并完成、请求 2 晚到 Superseded → 状态与 LastRequestOutcome 仍指向
  请求 3，协调器如实记录 Superseded 计数。
- `R1P2_01_LateFailedRequest_DoesNotOverwriteCurrentOutcome`：旧请求晚到并准备
  失败（协调器落定 Failed）→ 当前请求状态不被覆盖。
- `R1P2_01_WorkspacesIndependent_IdentityOutcomeAndSnapshotIsolated`：两个
  Workspace 的身份计数、结果与快照互不影响。
- `R1P2_01_ClosedThenReopened_OldInstanceCannotPolluteNewInstance`：关闭后旧实例
  迟到结果不污染状态文本；重开后新实例身份从 1 独立计数，旧实例保持冻结。

## 3. R1-P2-02 — ViewportGeneration 与初始场景请求时序（方案 B）

### 裁定与依据

**采用方案 B**（经用户确认）：当前 M1 空白场景快照与视口尺寸无关。

依据：

1. `BlankSceneContent` 只含背景 token，不含任何视口尺寸输入；
2. `MapSceneRenderer` 为整画布 `Clear`，绘制输出不依赖帧宽高；
3. PRA-01 M1 Profile §3.5：请求创建时把代捕获为不可变值——捕获的代是请求身份
   的一部分（canonical 指纹含 `ViewportGeneration`），不要求快照代与当前代一致；
4. 初始请求先于 `MapViewportControl.Bind` 发起是合法时序；Bind →
   `PushViewportSize` → 代数推进不构成已发布快照的场景失效。

### 实现与契约落点

- `MapViewportBinding.OnViewportChanged` 文档化契约：代数推进只影响未来捕获帧
  的代标记；不产生场景请求；未来依赖视口的场景内容（WP-30C / B 阶段）落地时
  必须在本方法建立**去重刷新路径**（按内容对视口的真实依赖判定），不得借本方法
  无条件增加场景请求。
- `ProjectWorkspaceViewModel.OpenWorkspaceAsync` 注释标注初始请求先于 Bind 的
  时序与契约 B 的关系。

### 测试（`ViewportGenerationTimingTests`）

- `R1P2_02_InitialRequestBeforeBind_GenerationAdvanceIsNotSceneInvalidation`：
  初始请求（代 1）发布 → Bind 推进到代 2 → 无新请求、无新发布、快照身份不变；
  CaptureFrame 如实携带双方代数（当前 2 / 快照请求 1），差异为合法状态。
- `R1P2_02_RepeatedViewportNotifications_DoNotGenerateRequests`：Bind + 5 次尺寸
  变化 + 1 次重复尺寸 → 请求次数恒为 1。
- `R1P2_02_NoEffectiveViewportSize_SnapshotStillPublishedAndCapturable`：宽高为 0
  时初始请求照常发布、捕获行为正常；首次 Bind 不产生请求。
- `R1P2_02_ViewportGenerationIndependentAcrossWorkspaces`：代计数器每视口私有，
  双向推进互不影响。

## 4. R1-P2-03 — Renderer 真实 Skia 像素测试

### 修复

新增 `MapSceneRendererPixelTests`：真实 `MapSceneRenderer`（不覆写 `Draw`）在
Skia CPU Surface（`SKSurface.Create`，纯软件光栅，无 GPU / 平台图形设备 / GUI
依赖）上执行基类绘制路径，对**实际像素输出**断言：

| 测试 | 断言 |
|---|---|
| `Draw_NullSnapshot_ProducesEmptyStateBackgroundPixels` | null Snapshot → `#202226`（空态契约） |
| `Draw_PublishedNeutralSnapshot_ProducesNeutralBackgroundPixels` | 真实管线发布的 BuiltinNeutralV1 快照 → `#E8E9EB` |
| `Draw_EmptyStateVsPublished_PixelOutputsDistinguishable` | 两态实际像素可区分 |

资源（Surface / Pixmap / Runtime）全部就地释放。既有 `RecordingRenderer` 测试
保留（验证快照引用传递与帧捕获语义）。本组测试不是地图几何绘制测试。

## 5. 架构边界确认（未改变任何既有契约）

- Application 不依赖 Avalonia（`IUiDispatcher` 仅在 Desktop）；Domain 零依赖不变；
- 每 Workspace 独立 Binding / Runtime / Publisher / Coordinator / ViewportGeneration
  不变；`CaptureFrame` 仍是唯一捕获点；Renderer 仍只消费不可变快照；
- Coordinator latest-request-wins、C1/C2 生命周期、F-1 关闭屏障、关闭重开实例
  隔离语义全部保持；A-01～A-10 全部通过且未被弱化；
- 无 Pan/Zoom / DPI 专项 / 地图几何 / Selection / Inspector 越界实现。

## 6. 测试结果

| 配置 | 结果 | 备注 |
|---|---|---|
| Desktop.Tests（Debug） | 90/90 × 4 连续运行 | 含 A-01～A-10 与全部新增测试 |
| 全解决方案 Debug | 975/975 | 基线 961 + 14（新增 7+4+3） |
| 全解决方案 Release | 971/971 | 与 Debug 差 4 = `DebugCloseFaultTests.cs`（`#if DEBUG` 包裹的 4 个 Fact，既有差异，非本次引入） |
| 四矩阵 CI | 见 PR #40 Checks | Windows/macOS × Debug/Release |

## 7. 遗留 / 范围外

- Viewport 依赖内容的去重刷新路径：WP-30C / B 阶段（本任务仅落契约，不提前实现）；
- Pan/Zoom、DPI 专项适配、地图几何投影：范围外，未触碰；
- `WorkspaceStatusText` 为项目级单状态字段，多 Workspace 并发切换时的最终显示
  归属"最后完成的有效请求"——如需按工作区分列状态，属后续 UI 需求，不在本任务。
