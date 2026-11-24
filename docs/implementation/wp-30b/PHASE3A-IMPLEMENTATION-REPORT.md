# WP-30B Phase 3A — 实施报告（IMPLEMENTATION-REPORT）

- **Date**: 2026-11-24
- **分支**: `feat/wp-30b-phase3a-lifecycle`（自 master `d2dee0a` 切出）
- **状态**: 实施完成，待独立复审（PR 未合并）
- **配套文档**: `PHASE3A-IMPLEMENTATION-PLAN.md`（§10/§11 为实施调整与本地验证记录）

---

## 1. 最终 HEAD SHA

提交后回填（见 §10 提交信息）：本次实施为**单一提交**，分支 HEAD 即交付物。

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

**新增 19 项**（Desktop.Tests 17 + Application.Tests 2），全部验证真实调用顺序（程序序锚点 / TCS 门控，无 Sleep / 轮询）：

- Binding：创建/Attach、Bind 拒绝已关闭、Detach 零释放、Detach→Reattach 复用同一 Binding、关闭阶段顺序（`is_closed → coordinator_closed → publisher_disposed → renderer_disposed` + Dispose 入口探针）、重复 Close 幂等、终态不可重用、CaptureFrame 组合、代推进。
- C1：Binding 先于工作区关闭的顺序证明（门控锚点时刻会话仍 Ready + 工作区未关闭）、兄弟工作区/会话/AssetStore/Database 不受影响、工作区 CTS 释放。
- C2：全部 Binding 逐一关闭后才关会话（两个门控锚点时刻会话均 Ready）、命令+直调+并发汇聚单次执行（ProjectClosed==1、DisposeCount==1）、ProjectClosed 先于任务完成的 R5 交错、失败异常被观察 + finished 标志 + 不触发 ProjectClosed、启动前取消拒绝、命令路径失败呈现。
- Application：会话关闭释放子工作区 CTS（Token 访问 ODE）、重复 Close 不二次释放。

**本地结果**（沙箱内直跑测试可执行文件；`dotnet test` 的 NamedPipe IPC 被沙箱阻断）：

| 项目 | 结果 |
|---|---|
| WorldView.Desktop.Tests | **30/30 通过** |
| WorldView.Presentation.Tests | **111/111 通过** |
| WorldView.Domain.Tests | **266/266 通过** |
| WorldView.Application.Tests | 生命周期测试全部通过；**10 个 FieldPackage 测试因沙箱拒绝写 `%TEMP%` 失败（UnauthorizedAccessException），与本次变更无关** |
| WorldView.Infrastructure.Tests | 6 个 FieldPackage 导入导出测试，同为沙箱 `%TEMP%` 拒写 |

（失败的两类均为环境性；本次 diff 不含任何 packaging 代码。CI 双平台为权威判定。）

## 7. CI 结果

push 后于 GitHub Actions 四矩阵（Windows/macOS × Debug/Release）验证——结果在提交后回填本报告与最终答复。

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
