# WP-30B Phase 3A — 实施前核查报告（IMPLEMENTATION-PLAN）

- **Date**: 2026-11-24
- **基线**: master `d2dee0a`（PR #30 已合并，含 Phase 2 全部 5 份 ADR + 审查报告 R1–R5）
- **状态**: 只读核查完成，**未修改任何代码**；等待实施确认
- **建议分支**: `feat/wp-30b-phase3a-lifecycle`（自 master `d2dee0a` 切出）

---

## 1. 当前实现状态（总览）

| 组件 | 状态 | 位置 |
|---|---|---|
| `ViewportPresentationRuntime`（视口运行时：Publisher + Coordinator + Generation） | ✅ 已存在（WP-30A） | `WorldView/src/WorldView.Presentation/Publication/ViewportPresentationRuntime.cs` |
| `SceneSnapshotPublisher`（含 F-1 关闭屏障 `MarkCoordinatorClosed`，internal） | ✅ 已存在，测试锁定 | 同目录 `SceneSnapshotPublisher.cs` |
| `PresentationRequestCoordinator`（`CloseAsync` 停新请求 + 协作取消；`DisposeAsync` 幂等） | ✅ 已存在 | 同目录 `PresentationRequestCoordinator.cs` |
| `ViewportGenerationTracker`（单调代计数） | ✅ 已存在 | 同目录 `ViewportGenerationTracker.cs` |
| `SceneSnapshot`（不可变，`ImmutableArray` 图层，不含相机/DPI） | ✅ 已存在 | `WorldView.Presentation/Scene/` |
| `WorkspaceViewState`（可变占位：CameraX/Y/Zoom/ActiveTool） | ✅ 已存在，符合 ADR-11「保留不动」 | `WorldView.Application/Sessions/MapWorkspaceSession.cs:109-125` |
| **`MapViewportControl`** | ❌ **不存在** | — |
| **`MapViewportBinding`** | ❌ **不存在** | — |
| **`MapSceneRenderer`** | ❌ **不存在** | — |
| **`ViewportFrameInput`（不可变帧输入）** | ❌ **不存在** | — |
| ViewModel 存活 Binding 集合 + `CloseProjectLifetimeAsync()` | ❌ 不存在 | — |
| ProjectWindow `Closing`/`Closed` 处理器 | ❌ 不存在（`ProjectWindow.axaml.cs` 全文无） | — |
| Desktop → Presentation 工程引用 | ❌ **缺失**（见 §4-A1，实施前置条件） | `WorldView.Desktop.csproj` |

结论：Phase 3A 是**从零创建 Desktop 视口层**（4 个新类型）+ **改造 2 个既有文件**（ViewModel、ProjectWindow）+ **1 处工程引用**；Presentation / Domain / Application 的呈现侧代码不需要动（除一个决策点，见 §8-1）。

## 2. 实际文件与类的对应关系

```
WorldView.Desktop（新增代码全部在此；Avalonia 12.1.6 直接依赖，SkiaSharp 3.119.4 传递依赖已就位）
 ├─ ViewModels/ProjectWorkspaceViewModel.cs     ← 改造：C1/C2 编排 + Binding 集合
 ├─ Views/ProjectWindow.axaml(.cs)              ← 改造：Closing 取消-编排-重关 + 视口宿主区
 └─ Rendering/（建议新目录，全部 internal）
     ├─ MapViewportControl.cs                   ← 新建：Avalonia Control
     ├─ MapViewportBinding.cs                   ← 新建：生命周期单一执行者
     ├─ MapDrawOperation.cs                     ← 新建：ICustomDrawOperation 短生命周期对象
     ├─ MapSceneRenderer.cs                     ← 新建：无状态最小绘制（M1 空态即可）
     └─ ViewportFrameInput.cs                   ← 新建：不可变帧输入值

WorldView.Presentation（只读消费，零修改）
 └─ Publication/ViewportPresentationRuntime.cs  ← Binding 持有（Create() 工厂 / DisposeAsync）

WorldView.Application（只读消费；1 个决策点见 §8-1）
 ├─ Sessions/ProjectSession.cs                  ← CloseWorkspaceAsync / CloseAsync / DisposeAsync
 └─ Sessions/MapWorkspaceSession.cs             ← CloseAsync / DisposeAsync / ViewState

tests/
 ├─ WorldView.Desktop.Tests/                    ← 新增生命周期测试（xunit v3；已引用 Desktop 工程）
 │   ├─ Testing/Fakes.cs                        ← 需补 FakeWorkspaceContextResolver
 │   └─ ViewModels/ProjectWorkspaceViewModelTests.cs（既有，风格参照）
 └─ WorldView.Presentation.Tests/               ← 不动；F-1 竞态测试已锁定关闭顺序语义
```

## 3. 现有生命周期调用链（逐行核实）

**C1（现状，`ProjectWorkspaceViewModel.CloseWorkspaceAsync`，131–145 行）**：
`Session.CloseWorkspaceAsync(workspaceId)` → `Workspaces.Remove(workspace)` → `workspace.DisposeAsync()`。
**缺**：第 1 步之前没有 Binding 关闭（Binding 尚不存在）；顺序与 ADR-10 §7.1 一致（先 Application 后本地清理）。

**C2（现状，`CloseProjectAsync`，150–156 行）**：
`await Session.CloseAsync(ct)` → `ProjectClosed?.Invoke()` →（`ProjectWindow.axaml.cs:26-29` 经 DataContext PropertyChanged 订阅）`Window.Close()`。
**缺**：先关会话后关窗口（R4 已判定顺序错误）；无唯一编排入口；无幂等；窗口 X 无任何编排。

**`ProjectSession.CloseAsync`（204–253 行）**：置 `Closing` → 循环关闭并移除全部子工作区（**只调 `workspace.CloseAsync()`，不调 `DisposeAsync()`** —— P2 既有缺口）→ `AssetStore.DisposeAsync()` → `Database.DisposeAsync()` → 置 `Closed` → 触发 `SessionClosed`。

**`MapWorkspaceSession`**：`CloseAsync` 置 `_closed` + 取消 `_derivedWorkCts`；`DisposeAsync` 释放 CTS（CTS.Dispose 幂等，重复 Dispose 安全）。

**停机兜底（`Program.cs:41-45`）**：消息循环退出后 `registry.CloseAllAsync().GetAwaiter().GetResult()` —— 进程级兜底，绕过 C2 编排（Phase 2 R5 已如实记录，本轮不改）。

**F-1 屏障（已测试锁定）**：`Coordinator.CloseAsync()` → `publisher.MarkCoordinatorClosed()`（发布器锁内线性化点）→ `Publisher.Dispose()`；`Runtime.DisposeAsync()` 已按此顺序封装（`ViewportPresentationRuntime.cs:56-60`）。

## 4. 与 ADR 不一致的地方（实施时必须补齐/修正）

| # | 差异 | ADR 依据 | 处置 |
|---|---|---|---|
| A1 | `WorldView.Desktop.csproj` **不引用** `WorldView.Presentation`——Binding 无法持有 `ViewportPresentationRuntime` | ADR-07 组件图（Binding 持有 Runtime）；分层约束「Avalonia/Skia 只在 Desktop」不受影响 | 加 ProjectReference（唯一工程引用变更） |
| A2 | `CloseProjectAsync` 先 `Session.CloseAsync()` 后 `ProjectClosed`，与 ADR-10 §7.2 要求的「先关全部 Binding → 关会话 → 清集合 → 通知窗口」相反 | ADR-10 §7.2 / 审查报告 §3C 第 8 项 | 改写命令体为委托唯一编排方法 |
| A3 | 无 `CloseProjectLifetimeAsync()`、无幂等状态、无存活 Binding 集合 | ADR-10 §7.2 / §8B 第 25 条 | ViewModel 新增（internal 可见性由测试经 InternalsVisibleTo 访问） |
| A4 | ProjectWindow 无 `Closing` 处理器；按 R5 需「取消本次关闭 → 接入编排 → 完成后重关」 | ADR-10 §7.2 窗口关闭流程（R5） | View 层新增薄壳处理器（见 §6-W3） |
| A5 | Desktop 无 `InternalsVisibleTo` 授权测试程序集；Binding 按约束 7 必须 internal | ADR-07 约束 7 | `WorldView.Desktop.csproj` 加 `InternalsVisibleTo WorldView.Desktop.Tests`（与 Application/Presentation 既有模式一致） |
| A6 | `ProjectSession.CloseAsync` 不调子工作区 `DisposeAsync()` | Phase 2 风险表「实现阶段单独裁决修复方式并补幂等测试」 | **决策点 §8-1**，建议本轮修复 |

无其他不一致：`WorkspaceViewState` 可变占位符合 ADR-11；`SceneSnapshot` 不含视口标量符合 ADR-11；Presentation 无 Avalonia/Skia 引用符合 `PresentationBoundaryTests`。

## 5. 计划修改的文件清单

**新增**（6 个代码文件 + 3 个测试文件）：

| 文件 | 内容 |
|---|---|
| `Desktop/Rendering/MapViewportBinding.cs` | internal sealed：持有 `ViewportPresentationRuntime` + `MapSceneRenderer` + 缓存尺寸/DPI/相机标量来源；`volatile bool IsClosed`；`Attach(Control)` / `Detach()` / `CaptureFrame()` / `OnViewportChanged(...)` / `RequestClose()`（C1/C2 共用的 Binding 关闭子序：IsClosed → Coordinator（F-1）→ Publisher → Renderer，经 Runtime + Renderer 调用序实现）；不实现接口、不注册 DI |
| `Desktop/Rendering/MapViewportControl.cs` | internal Control：`Render` 第一件事查 `binding.IsClosed`；`AttachedTo/DetachedFromVisualTree` 只置挂载状态（Detach 不关闭）；`SizeChanged`/`ScalingChanged` → `binding.OnViewportChanged`；绘制经 `DrawingContext.Custom(new MapDrawOperation(binding.CaptureFrame(), renderer))` |
| `Desktop/Rendering/MapDrawOperation.cs` | 每帧短生命周期 `ICustomDrawOperation`：持不可变帧输入；`Render(ImmediateDrawingContext)` 内 `using` lease → 委托 `MapSceneRenderer.Draw(canvas, frame)`；`Equals` 按帧输入值语义（实现期锁定，Phase 2 OQ#4 可留最小实现） |
| `Desktop/Rendering/MapSceneRenderer.cs` | 无状态：`Draw(SKCanvas, ViewportFrameInput)` 发出最小绘制命令（null 快照 → 空态背景填充）；不持字段、不持 Skia 资源 |
| `Desktop/Rendering/ViewportFrameInput.cs` | `readonly record struct`：Snapshot 引用 + Width/Height + RenderScaling + CameraX/Y/Zoom + ViewportGeneration |
| `Desktop/Rendering/` 内或复用既有 | （Renderer 构造注入测试替身所需的 internal 抽象点——**不新增公共接口**，用 internal delegate/抽象类，仅程序集内可见） |
| `tests/WorldView.Desktop.Tests/Lifecycle/MapViewportBindingTests.cs` | 见 §7 |
| `tests/WorldView.Desktop.Tests/Lifecycle/ProjectCloseOrchestrationTests.cs` | 见 §7 |
| `tests/WorldView.Desktop.Tests/Testing/Fakes.cs`（补） | `FakeWorkspaceContextResolver`（成功解析，参照 Application.Tests 既有替身）+ 调用序记录器 |

**修改**（5 个既有文件）：

| 文件 | 内容 |
|---|---|
| `WorldView.Desktop.csproj` | + `ProjectReference WorldView.Presentation`；+ `InternalsVisibleTo WorldView.Desktop.Tests`；**不新增 NuGet 包**（SkiaSharp 3.119.4 已传递就位，ADR-07 约束 1） |
| `ViewModels/ProjectWorkspaceViewModel.cs` | ① `CloseProjectAsync` 改为只 `await CloseProjectLifetimeAsync()`；② 新增 `CloseProjectLifetimeAsync()`（唯一编排：单次启动字段 + 关全部 Binding → `Session.CloseAsync()` → 清集合 → `ProjectClosed` 一次）；③ `CloseWorkspaceAsync` 前插 `binding.RequestClose()`；④ 存活 Binding 集合（`Dictionary<Guid workspaceId, MapViewportBinding>`，仅用于枚举，非注册表）；⑤ 打开工作区时经工厂创建 Binding（每 workspace 一个） |
| `Views/ProjectWindow.axaml.cs` | 新增 `Closing` 处理器：编排已完成 → 放行；未启动/进行中 → `e.Cancel = true` + 接入唯一编排 + `await`（薄壳 async void，异常就地 try/catch 记日志）+ 完成后收尾关闭；防重入（见 §6-W3）；`Closed` 不新增编排接线 |
| `Views/ProjectWindow.axaml` | 工作区宿主区：选中工作区显示对应 `MapViewportControl`（单视口 M1，1 workspace = 1 viewport = 1 binding） |
| `Application/Sessions/ProjectSession.cs` | **决策点 §8-1**：`CloseAsync` 子工作区循环内补 `await workspace.DisposeAsync()`（仅当用户确认） |

## 6. 关键实现方案（实施确认后照此执行）

**W1 — Binding 关闭子序与可测性**：`RequestClose()` 内部顺序 = 置 `IsClosed` → `Runtime.Coordinator.CloseAsync()`（F-1 屏障线性化）→ `Publisher.Dispose()` → `Renderer.Dispose()`。Renderer 以 internal 注入点传入，测试用记录序替身断言 `IsClosed → coordinator closed → publisher disposed → renderer disposed` 的真实先后（复用 `CoordinatorClosePublicationRaceTests` 的「程序序锚点 + 门控、无 Sleep」风格）。Runtime 的 `DisposeAsync` 已封装 Coordinator+Publisher 两步，Binding 直接编排两步以保持 ADR-10 §7.1 的四步可见顺序。

**W2 — C2 编排与幂等**：`CloseProjectLifetimeAsync()` 用单个私有 `Task? _lifetimeCloseTask` 字段：首次调用创建并保存任务；后续调用 `await` 同一任务返回（不并行、不跳过、不重复关会话）。CancellationToken 只用于启动前拒绝（会话非 Ready），启动后以 `None` 跑到结束。编排步骤严格按 ADR-10 §7.2 步骤 0–5。

**W3 — Window Closing 重入（Phase 2 R5 方案的落地）**：
- 关键清理逻辑全部在 `CloseProjectLifetimeAsync()`（返回 `Task`），**不用 async void 承载清理**；`Closing` 处理器只是事件适配薄壳（Avalonia 事件签名强制 void，async void 壳内 try/catch 就地观察异常并记日志，满足「异常被观察」）。
- 处理器逻辑：`vm` 编排已完成 → 放行；否则 `e.Cancel = true`，启动/接入编排并 `await`。
- **收尾关闭统一由编排步骤 4 的 `ProjectClosed → Window.Close()` 承担**（该订阅已存在，`ProjectWindow.axaml.cs:26-29`）；处理器 await 完成后**仅在窗口仍打开时**补一次 `Close()` 兜底。两次潜在 `Close()`（ProjectClosed 的与兜底的）汇聚到同一放行判断（编排已完成 → 放行），不会二次取消、不会死循环；重复 `Close()` 的安全性在实现期以测试锁定（若 Avalonia 12.1.6 对已关窗口 `Close()` 抛异常，则以「窗口仍打开才调」的守卫消除，属实现细节，不改 ADR 口径）。
- `Closed` 不新增编排接线（ADR-10 约束 15）。

**W4 — Attach/Detach**：`MapViewportControl.AttachedToVisualTree` → `binding.Attach(this)`（已 `IsClosed` 的 binding 拒绝并记诊断）；`DetachedFromVisualTree` → `binding.Detach()`（只置挂载状态，零释放）；重新挂载复用同一 binding。控件销毁不隐式关闭 Binding——永久关闭只由拥有者（ViewModel 的 C1/C2）显式触发。

**W5 — 渲染最小实现**：本阶段不发起场景请求（`RequestSceneAsync` 需要 `PresentationContext` 组装，属 WP-30C 交互阶段）；帧输入的 `Snapshot` 在 M1 恒为 null（`Publisher.Current` 从未发布），Renderer 只画空态背景。生命周期正确性不受影响，F-1/捕获点/关闭检查全部就位。

## 7. 需要新增的测试（全部验证真实调用顺序，非只验终态）

**MapViewportBindingTests**（经 InternalsVisibleTo 访问 internal；Runtime 用真实实例 + 门控替身）：
1. 创建后正常 Attach（控件挂载态、Render 入口可捕获帧）。
2. Attach → Detach → Attach：复用**同一** Binding 实例；Detach 前后 `IsClosed`/Runtime 状态不变。
3. Detach 不触发任何关闭（Coordinator 未关、Publisher 未释放、Renderer 未 Dispose——以替身计数断言）。
4. 永久 Close 后不可重新 Attach（拒绝 + 诊断）；`IsClosed` 终态不可重置。
5. 重复 Close 幂等：Renderer.Dispose / Publisher.Dispose 各只一次。
6. 关闭顺序锚点：`IsClosed → coordinator closed → publisher disposed → renderer disposed` 的程序序断言（门控，无 Sleep）。
7. 已关闭 Binding 不被再次使用（CaptureFrame 返回后 Render 入口命中 `IsClosed` 即返回——以可观测计数验证）。

**ProjectCloseOrchestrationTests**（真实 ProjectSession + Fakes；workspace 经 `OpenWorkspaceAsync` 真实打开）：
8. C1 只关闭目标 Workspace：兄弟 workspace 状态不变、`Session.IsReady` 保持。
9. C1 顺序：binding 关闭 → `CloseWorkspaceAsync` → `Workspaces.Remove` → `workspace.DisposeAsync`（序记录器）。
10. C1 不触碰 AssetStore/Database（Stub 计数断言未释放）。
11. C2 关闭**全部**存活 Binding（多 workspace 逐个序断言）后才调 `Session.CloseAsync`（会话 `Closing` 状态在 Binding 关闭后才开始——以状态锚点验证）。
12. C2 只执行一次：`CloseProjectLifetimeAsync` 与 `CloseProjectAsync` 连续/并发触发只跑一套（并发用 TaskCompletionSource 门控制造交错，无延时）。
13. `ProjectClosed` 只触发一次。
14. 多次编排调用等待首次操作（第二个调用在首次完成前被挂起、完成后共同返回）。
15. C2 失败（注入 `StubPackageDatabase.DisposeAsync` 抛异常）：异常被编排观察/记录，`ProjectClosed` 语义与窗口关闭策略符合设计（不产生未观察异常——以测试内 await 验证）。
16. 子工作区 Dispose（若 §8-1 确认修复）：`ProjectSession.CloseAsync` 后每个 workspace 的 CTS 已释放。

**窗口 Closing 流程**（View 层薄壳不引入 Avalonia Headless 测试框架——不新增测试包；重入/取消语义由 VM 层幂等契约测试 11–15 + 代码审查覆盖；真实窗口行为列入 macOS/Windows 手工验证项）。

## 8. 风险与尚未解决的问题

1. **〔需确认〕`ProjectSession.CloseAsync` 子工作区 Dispose 缺口（P2）**：Phase 2 风险表授权「实现阶段单独裁决」。本轮修复（循环内补 `DisposeAsync` + 测试 16）只改 Application 一处循环体，不动技术边界；但严格说超出「Desktop 接线」范围。**建议：本轮一并修复**，请确认。
2. **已关窗口重复 `Close()` 的 Avalonia 行为未验证**：以 W3 的守卫方案规避，不影响生命周期正确性；实现期实测确认。
3. **`MapDrawOperation.Equals` 失效语义**：Phase 2 OQ#4 未决；M1 每帧新建 draw operation（无失效合并），本阶段给最小实现 + 注释，不阻塞。
4. **View 层 Closing 处理器无法无头测试**：不引入 Avalonia.Headless 测试包（新增依赖需另行确认）；以 VM 层契约测试 + CI 双平台构建 + 手工验证补足。若要求自动化 UI 测试，需单独引入 `Avalonia.Headless.XUnit`，请确认是否允许。
5. **CI 触发**：新分支名不在 `dotnet-ci.yml` 的 push 分支列表中——PR 以 master 为 base 时 `pull_request` 触发照常运行四矩阵，无需改 workflow。
6. **不可取消的第一次 Closing 竞态**：用户点 X 的同一瞬间命令路径恰好完成编排——两路都汇聚到「已完成 → 放行」分支，无死锁窗口；测试 12/13 覆盖交错。

## 9. 待确认清单（阻塞实施）

1. §8-1：是否本轮修复 `ProjectSession.CloseAsync` 子工作区 Dispose 缺口（建议：是）。
2. §8-4：是否允许引入 `Avalonia.Headless.XUnit` 做 Closing 处理器自动化测试（建议：否，本阶段以 VM 契约测试 + 手工验证为准）。
3. 分支名 `feat/wp-30b-phase3a-lifecycle` 是否认可。

**核查结束，未修改任何代码；等待实施确认。**

---

## 10. 实施调整记录（2026-11-24，实施完成后回填）

方案已获批准（三条决策均按建议执行）。实施中发生以下必要调整，均不改变批准的架构裁决：

| # | 调整 | 理由 |
|---|---|---|
| 1 | **§6-W3 的 Closing 处理器从"薄壳 async void"改为同步 void + 后台观察 Task**：处理器本身不 await（同步设置 `e.Cancel` 并启动等待者），等待与收尾在 `AwaitCloseLifetimeAndRecloseAsync`（async Task，全捕获）中完成 | 任务约束"不使用 async void 承载关键清理逻辑"的最严格满足：全工程无 async void 承载清理（唯一保留的 async void 是既有的 `OnCreateMapClick` 对话流程，非生命周期路径） |
| 2 | **Closing 重入最终方案（§6-W3 细化）**：`IsProjectCloseLifetimeFinished` 在编排 `finally` 中置位（晚于步骤 4 的 ProjectClosed）→ ProjectClosed 触发的 `Close()` 在编排结束前被取消；收尾重关由**首个等待者**执行（`_closingAwaitedOnce` / `_finalCloseIssued` 双守卫），后续重入 Closing 只等待不收尾 | ProjectClosed 在 C2 任务完成**之前**触发（已由测试锁定该交错）；双守卫保证"最终窗口关闭只有一个成功执行者"且不会无限重复 Close() |
| 3 | **C2 幂等实现采用 TCS + Interlocked CAS**：先写入共享任务字段再执行编排体，编排体异常全部路由进 TCS（编排体自身的返回 Task 永不 fault，可安全丢弃） | 保证"即使编排体在当前调用栈同步跑完（ProjectClosed → Close → Closing 重入链），重入也只会看到已启动"；单线程 UI 与并发测试两种场景都成立 |
| 4 | **门控测试的编排触发移至 `Task.Run`** | 生产 renderer.Dispose 是幂等空操作不阻塞；测试替身的门控会同步阻塞调用线程（所有 await 同步完成），必须有独立线程放行门控。属测试侧交错构造，非生产代码 |
| 5 | **实例同一性断言改为两次直调比对**（`Assert.Same(direct2, direct3)`） | `Task.Run(Func<Task>)` 的 Unwrap 产生包装任务实例，不能与直调任务做实例比对；汇聚证据由"ProjectClosed 只触发一次 / DisposeCount==1"给出 |
| 6 | **既有测试 `CloseSession_ClosesChildrenFirst_ThenAssets_ThenDatabase` 更新**：取消断言改为关闭前捕获令牌，关闭后断言令牌已取消 + Token 访问抛 ODE | 子工作区 Dispose 缺口修复（决策 1）的合法语义变化：会话关闭后 CTS 已释放 |
| 7 | 视口宿主区加入 `ProjectWindow.axaml`（选中工作区显示对应 `MapViewportControl`），接线由 code-behind 按 `SelectedWorkspace` 变化重建 | 计划 §5 预留项的落地；M1 单视口 |

## 11. 实施验证结果（本地）

- 解决方案构建：0 错误（警告均为既有）。
- `WorldView.Desktop.Tests`：**30/30 通过**（新增 9 Binding 生命周期 + 8 编排/交错测试）。
- `WorldView.Application.Tests`：生命周期测试全部通过（含新增 2 项子工作区 Dispose 测试）；**10 个 FieldPackage 写 `%TEMP%` 失败为沙箱环境拒绝（UnauthorizedAccessException），与本次变更无关**，CI 双平台将验证。
- `WorldView.Presentation.Tests`：111/111 通过。`WorldView.Domain.Tests`：266/266 通过。
- `WorldView.Infrastructure.Tests`：6 个 FieldPackage 导入导出失败，同为沙箱 `%TEMP%` 拒写（与本变更无关）。
- 本地无法执行的项：真实窗口手工验证（Closing 全流程）、CI 双平台——见 REPORT。
