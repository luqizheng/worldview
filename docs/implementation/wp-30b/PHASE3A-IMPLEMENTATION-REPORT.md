# WP-30B Phase 3A — 实施报告（IMPLEMENTATION-REPORT）

- **Date**: 2026-11-24
- **分支**: `feat/wp-30b-phase3a-lifecycle`（自 master `d2dee0a` 切出）
- **状态**: 实施完成，待独立复审（PR 未合并）
- **配套文档**: `PHASE3A-IMPLEMENTATION-PLAN.md`（§10/§11 为实施调整与本地验证记录）

---

## 1. 最终 HEAD SHA

- **实现提交**: `2cd2122` — `feat(wp-30b): phase 3A viewport control, binding and lifecycle wiring`（16 文件，+1770/−9）
- **文档回填提交**: 本节与 §7 的 CI 结果由其后一个 docs-only 提交写入（见分支最新提交）
- **基线**: master `d2dee0a`；**PR**: https://github.com/luqizheng/worldview/pull/31（未合并）

## 2. 变更文件清单

**新增**（8 个）：

| 文件 | 说明 |
|---|---|
| `WorldView/src/WorldView.Desktop/Rendering/MapViewportBinding.cs` | 视口生命周期单一执行者（internal sealed） |
| `WorldView/src/WorldView.Desktop/Rendering/MapViewportControl.cs` | Avalonia 控件（internal sealed） |
| `WorldView/src/WorldView.Desktop/Rendering/MapDrawOperation.cs` | 每帧 ICustomDrawOperation（internal sealed） |
| `WorldView/src/WorldView.Desktop/Rendering/MapSceneRenderer.cs` | 无状态最小渲染器（internal，Dispose 幂等空操作） |
| `WorldView/src/WorldView.Desktop/Rendering/ViewportFrameInput.cs` | 不可变帧输入（readonly record struct） |
| `WorldView/tests/WorldView.Desktop.Tests/Lifecycle/MapViewportBindingTests.cs` | 9 项 Binding 生命周期测试 |
| `WorldView/tests/WorldView.Desktop.Tests/Lifecycle/ProjectCloseOrchestrationTests.cs` | 8 项 C1/C2 编排与交错测试 |
| `docs/implementation/wp-30b/PHASE3A-IMPLEMENTATION-PLAN.md` | 核查报告 + 实施调整记录（§10/§11） |

**修改**（7 个）：

| 文件 | 说明 |
|---|---|
| `WorldView.Desktop.csproj` | +Presentation 工程引用；+InternalsVisibleTo（ADR-07 约束 1/7；**无新 NuGet 包**） |
| `ProjectWorkspaceViewModel.cs` | C1 前置 Binding 关闭；C2 唯一编排 `CloseProjectLifetimeAsync()`（TCS+CAS 幂等）；存活 Binding 字典；`IsProjectCloseLifetimeFinished` |
| `ProjectWindow.axaml.cs` | Closing 取消-编排-重关（同步处理器 + 后台观察 Task + 双守卫收尾）；SelectedWorkspace → 视口宿主接线 |
| `ProjectWindow.axaml` | 视口宿主区（ContentControl） |
| `ProjectSession.cs`（Application） | CloseAsync 子工作区循环内补 `DisposeAsync()`（批准的 P2 缺口修复，+4 行） |
| `Fakes.cs`（Desktop.Tests） | +FakeWorkspaceContextResolver；Stub 数据库/资产库加释放计数与失败注入 |
| `SessionUseCaseTests.cs`（Application.Tests） | +2 项子工作区 Dispose 测试；1 项既有测试适配新语义 |

**未触碰**：Domain / Infrastructure / Presentation 源代码、依赖与 NuGet、公共 API 面（新增类型全部 internal）、`docs/map-editor/implementation/`。

## 3. 核心实现说明

- **MapViewportBinding**：持有 `ViewportPresentationRuntime` + 无状态 Renderer；`volatile bool IsClosed` 终态；`CaptureFrame()` 唯一捕获点（快照引用一次原子读取 + 视口标量 + ViewState 相机值 + 视口代，ADR-11）；`RequestClose()` 幂等（lock 内检查-置位，重复调用记 `already_closed`）；关闭子序 = 置 IsClosed → Coordinator.DisposeAsync（内含 F-1 屏障线性化）→ Publisher.Dispose → Renderer.Dispose，全程记录 `CloseStageLog`（诊断 + 测试程序序锚点）。Attach 拒绝已关闭 Binding（ADR-07 约束 10）；Detach 只解除引用。
- **MapViewportControl**：Render 第一件事做 volatile 关闭检查（true → 不创建 draw operation、不取 lease）；`DetachedFromVisualTree` 零释放（生命周期 A）；尺寸/DPI 经 `OnViewportChanged` 缓存并推进视口代。
- **MapDrawOperation**：`TryGetFeature(typeof(ISkiaSharpApiLeaseFeature))` → `using var lease = feature.Lease()` → 委托 Renderer（lease 只在 Render 调用栈内存活，ADR-09）；M1 只画空态背景，不发起场景请求（`Snapshot` 恒 null，请求组装属 WP-30C）。
- **C2 幂等**：TCS + `Interlocked.CompareExchange`——共享任务字段先置位、编排体后执行；编排体异常全部路由进 TCS（编排体自身 Task 永不 fault，丢弃安全，无未观察后台任务）；`CancellationToken` 仅用于启动前拒绝。
- **Application 修复**：`ProjectSession.CloseAsync` 每关闭一个子工作区后立即 `DisposeAsync`（先 Close 后 Dispose 次序不可交换；CTS.Dispose 幂等）。

## 4. C1 / C2 实际调用顺序（实现与测试锁定的真实顺序）

**C1**（`CloseWorkspaceCommand`）：
```text
binding.RequestClose()                     ← IsClosed → Coordinator(F-1) → Publisher → Renderer
ProjectSession.CloseWorkspaceAsync(id)     ← workspace.CloseAsync() → _workspaces.Remove
Workspaces.Remove(workspace)               ← ViewModel 集合
workspace.DisposeAsync()                   ← CTS 释放
```
不触碰兄弟工作区 / Session 状态 / AssetStore / Database（测试锁定）。

**C2**（`CloseProjectLifetimeAsync`，唯一编排入口）：
```text
0. CAS 占位共享任务（重入/并发一律等待同一任务）
1. 逐个关闭全部存活 Binding（同 B 子序；单个失败记诊断后继续）
2. Session.CloseAsync(CancellationToken.None)   ← Closing → 子工作区逐个 Close+Dispose → AssetStore → Database → Closed
3. _viewportBindings.Clear() + Workspaces.Clear()
4. ProjectClosed?.Invoke()（全程恰好一次）
finally: IsProjectCloseLifetimeFinished = true  ← 窗口 Closing 处理器此后一律放行
```

## 5. Window Closing 重入的最终处理方案与测试证据

**方案**（严格满足批准的 W3 八条要求）：

1. **唯一 C2 入口**：`CloseProjectLifetimeAsync()` 是唯一业务编排方法；命令路径与 Closing 都只经它。
2. **共享同一 Task**：TCS+CAS 保证两个入口拿到同一任务实例；测试 `C2_SharedTask…` 锁定。
3. **ProjectClosed → Close() 的时序**：ProjectClosed 在 C2 任务完成**之前**触发（步骤 4 先于 finally）——该次 `Close()` 会进入 Closing，因 `IsProjectCloseLifetimeFinished == false` 而被**安全取消**，窗口保持打开。交错已由测试 `C2_ProjectClosedFires_BeforeOrchestrationTaskCompletes_R5WindowContract` 以真实顺序锁定（事件触发瞬间：共享任务 Same 断言 + finished==false 断言）。
4. **重入 Closing 安全取消、不等待自身**：每次未完成时的 Closing 都取消并挂起等待**编排任务**（不是等待任何 Closing 处理器）——编排不依赖处理器完成，无自等待、无死锁；TCS 用 `RunContinuationsAsynchronously` 防同步恢复自旋。
5. **唯一收尾执行者**：`_closingAwaitedOnce`（首个等待者负责收尾）+ `_finalCloseIssued`（重关只发一次）双守卫；后续重入 Closing 只等待、不收尾、不重复 Close()。最终关闭 = 首个等待者在编排结束后发出的那次 `Close()`（此时放行）。
6. **无 async void**：Closing 处理器是同步 void（只设 Cancel + 启动等待者）；等待/收尾在 `async Task` 全捕获方法中；唯一的 async void（`OnCreateMapClick`）是非生命周期的既有对话框路径。
7. **失败策略**：编排体 catch 记日志后经 TCS 抛出 → 等待者就地观察；`finished` 标志在 finally 置位（成功/失败都算结束）→ 窗口仍收尾关闭（不卡死窗口、不留未观察异常）。命令路径在命令体捕获并呈现状态文本（`C2_CommandPath_CatchesFailure` 锁定）。
8. **无无限重复 Close**：`_finalCloseIssued` 保证收尾 Close 恰好一次；若编排已结束则 Closing 直接放行，不产生任何等待者。

**证据层级**：VM 层真实交错测试（上列）+ 编排/收尾代码路径单一 + 真实窗口全流程列为手工验证项（不引入 Avalonia.Headless，按批准决策 2）。

## 6. 测试覆盖与结果

**新增 19 项**（Desktop.Tests 17 + Application.Tests 2），全部验证真实调用顺序（程序序锚点 / TCS 门控，无 Sleep / 轮询）；独立复审后追加 5 项 DEF 修复测试（§10），Desktop.Tests 合计 35 项：

- Binding：创建/Attach、Bind 拒绝已关闭、Detach 零释放、Detach→Reattach 复用同一 Binding、关闭阶段顺序（`is_closed → coordinator_closed → publisher_disposed → renderer_disposed` + Dispose 入口探针）、重复 Close 幂等、终态不可重用、CaptureFrame 组合、代推进。
- C1：Binding 先于工作区关闭的顺序证明（门控锚点时刻会话仍 Ready + 工作区未关闭）、兄弟工作区/会话/AssetStore/Database 不受影响、工作区 CTS 释放。
- C2：全部 Binding 逐一关闭后才关会话（两个门控锚点时刻会话均 Ready）、命令+直调+并发汇聚单次执行（ProjectClosed==1、DisposeCount==1）、ProjectClosed 先于任务完成的 R5 交错、失败异常被观察 + finished 标志 + 不触发 ProjectClosed、启动前取消拒绝、命令路径失败呈现。
- Application：会话关闭释放子工作区 CTS（Token 访问 ODE）、重复 Close 不二次释放。

**本地结果**（沙箱内直跑测试可执行文件；`dotnet test` 的 NamedPipe IPC 被沙箱阻断）：

| 项目 | 结果 |
|---|---|
| WorldView.Desktop.Tests | **35/35 通过**（含 §10 的 5 项 DEF 修复测试） |
| WorldView.Presentation.Tests | **111/111 通过** |
| WorldView.Domain.Tests | **266/266 通过** |
| WorldView.Application.Tests | 生命周期测试全部通过；**10 个 FieldPackage 测试因沙箱拒绝写 `%TEMP%` 失败（UnauthorizedAccessException），与本次变更无关** |
| WorldView.Infrastructure.Tests | 6 个 FieldPackage 导入导出测试，同为沙箱 `%TEMP%` 拒写 |

（失败的两类均为环境性；本次 diff 不含任何 packaging 代码。CI 双平台为权威判定。）

## 7. CI 结果

GitHub Actions run `45391911222`（head `01aeefc`——DEF-01/DEF-02 修复提交，四矩阵全绿，输出仅既有 CTJ001 分析器警告）：

| Job | 结果 |
|---|---|
| windows-latest / Debug | ✅ success |
| windows-latest / Release | ✅ success |
| macos-latest / Debug | ✅ success |
| macos-latest / Release | ✅ success |

（初始实施提交 `2cd2122` / 文档回填 `07ba290` 的 run `45372890662` 同为四 job 全绿。）

## 8. 已知问题与后续工作

1. **真实窗口手工验证**（Closing 全流程、连点 X、Detach/Reattach、macOS 窗口行为）——CI 只构建不运行 UI。
2. `MapDrawOperation.Equals` 失效策略仍为最小实现（Phase 2 开放问题 4，不阻塞 M1）。
3. 场景请求仍未接线（`RequestSceneAsync` 需 PresentationContext 组装）——WP-30C 交互阶段。
4. `Program.Main` 停机兜底绕过 C2 编排的边界（Phase 2 R5 记录）未改写；正常路径中 C2 已在窗口关闭前完成。
5. 本地沙箱的 `%TEMP%` 拒写导致 16 个既有 packaging 测试无法本地复跑——CI 权威判定。

## 9. ADR 一致性检查结论

| ADR | 检查 | 结论 |
|---|---|---|
| ADR-07 | Binding Desktop 内部具体类、控件只持单引用、不暴露 Skia 类型、Attach 拒绝终态 | ✅ 全部 internal（`InternalsVisibleTo` 仅授权测试）；控件不持 Runtime/Publisher |
| ADR-08 | 无渲染线程/调度器；Render 入口关闭检查第一件事；在飞帧边界 | ✅ 无任何新线程/Task 常驻；volatile 检查在 `base.Render` 之前 |
| ADR-09 | M1 零长期 Skia 资源；lease 回调内 using；Renderer 无状态 | ✅ Renderer 零字段；`using var lease` 就地释放 |
| ADR-10 | 四分生命周期、B/C1/C2 关闭顺序、C2 唯一编排+幂等、R5 取消-编排-重关、停机兜底不改写 | ✅ §4/§5 的顺序与测试一一对应；子工作区 Dispose 缺口按批准修复（原 P2 关闭） |
| ADR-11 | WorkspaceViewState 不动；唯一捕获点；帧输入不可变、不含可变集合 | ✅ Application 的 ViewState 类型零修改；`readonly record struct` |

## 10. 独立复审修复记录（DEF-01 / DEF-02）

复审发现两个生命周期缺陷，已在分支上做最小修复（只动生产代码两处 + 测试，零架构变更）。

### DEF-01：`MapViewportBinding.RequestClose` 不共享进行中的关闭 Task

- **缺陷**：`IsClosed` 置位后重复调用直接返回 `Task.CompletedTask`——只证明"已启动"，不证明 Coordinator → Publisher → Renderer 子序已完成；首次关闭失败也被伪装成成功。
- **修复**（`MapViewportBinding.cs`）：新增 `_closeTask` 字段缓存唯一关闭 Task。第一次调用在 `_closeGate` 锁内创建并缓存；后续调用（进行中或已完成）返回同一 Task，只追加 `already_closed` 诊断。所有调用者等待相同结果，首次失败的异常经同一 faulted Task 原样传播。
- **锁内执行同步段的规避**：流水线经 `CloseDeferredStartAsync` 包装——`await Task.Yield()` 先把同步段（含门控测试中可阻塞的 `Renderer.Dispose`）调度出 `_closeGate`，并发调用者拿到同一 Task 而不会被卡在锁上（这一点由进行中交错测试锁定）。
- **保持不变**：`IsClosed` volatile 终态语义、B 子序顺序、F-1 屏障、`CloseStageLog`。

新增测试（`MapViewportBindingTests.cs`，均无 Sleep/轮询）：

1. `Close_InProgress_RepeatCallReturnsSameTask_AndWaitsForFirstCompletion`——门控 `Renderer.Dispose` 制造确定性交错；第二次调用在关闭进行中拿到同一 Task、`IsCompleted == false`，放行后两个等待者共同完成，`DisposeCount == 1`、`is_closed`/`renderer_disposed` 各恰好一条。
2. `Close_Failure_RepeatCallsObserveSameException_NotMaskedAsSuccess`——注入 Dispose 故障；两个调用者观察到**同一异常实例**（`Assert.Same`），`DisposeCount == 1`，终态不变。
3. `Close_InProgressFailure_RepeatCallSharesFaultedTask`——失败发生在门控放行后：进行中接入的重复调用者同样观察到原始异常。
4. 原 `Close_IsIdempotent_SecondCallRecordsAlreadyClosed` 增强：三次调用断言 `Assert.Same` 共享任务 + 各自可 await。

### DEF-02：C2 Task 完成早于 Finished 标志

- **缺陷**：`ExecuteProjectCloseLifetimeAsync` 中 `completion.SetResult/SetException` 在 `finally` 置 `_projectCloseLifetimeFinished = true` **之前**执行——Task 的异步等待者可能在 Finished 置位前恢复，导致窗口 Closing 再次取消最终关闭。
- **修复**（`ProjectWorkspaceViewModel.cs`）：重构完成路径——编排体不再在 try/catch 内完成任务，而是把异常存入 `failure` 局部量；**先写 volatile `_projectCloseLifetimeFinished = true`，再 `SetException(failure)` / `SetResult()`**。成功与失败同序。volatile 写先于 SetResult 的发布顺序，保证任何从 `completion.Task` 恢复的等待者必然读到 `Finished == true`。
- **保持不变**：TCS + CAS 唯一任务语义（`CloseProjectLifetimeAsync` 未动）、`ProjectClosed` 业务语义（仍在步骤 4、任务完成前触发）、现有 R5 测试全部保留且通过。

新增测试（`ProjectCloseOrchestrationTests.cs`）：

1. `C2_Success_FinishedFlagSet_BeforeTaskCompletion`——`ContinueWith(ExecuteSynchronously)` 观察者在任务完成时刻（最早观察点，经 TCS 确认观察者已运行后断言）读到 `Finished == true`。
2. `C2_Failure_FinishedFlagSet_BeforeTaskCompletion`——失败路径同序：异常观察者同样读到 `Finished == true`。
3. 现有 `C2_ProjectClosedFires_BeforeOrchestrationTaskCompletes_R5WindowContract` 继续锁定"ProjectClosed 时任务未完成 + Finished 为 false"（该断言要求 Finished 置位**晚于** ProjectClosed，与新不变量不矛盾：顺序是 ProjectClosed → Finished → SetResult）。

### 修复后测试结果

| 项目 | 结果 |
|---|---|
| WorldView.Desktop.Tests | **35/35 通过**（30 + 5 项 DEF 测试） |
| WorldView.Presentation.Tests | **111/111 通过** |
| WorldView.Domain.Tests | **266/266 通过** |
| WorldView.Application.Tests | 生命周期测试全部通过；10 个 FieldPackage 失败仍为沙箱 `%TEMP%` 拒写（环境性，与 DEF 修复无关） |

CI 结果见 §7（run `45391911222`，修复提交 `01aeefc` 四矩阵全绿）。
