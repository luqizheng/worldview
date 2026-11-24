# ADR WP-30B-10 — Workspace / Viewport 生命周期接线

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— `MapViewportBinding` 成为 Runtime / Renderer 的唯一持有者与关闭入口；控件只持有引用。
- **修订**: R2（2026-11-24）— 区分「暂时脱离视觉树」「视口永久销毁」「工作区显式关闭」三种生命周期；Detach 不再等同于关闭；修正"关闭后零绘制"的过度表述。
- **修订**: R3（2026-11-24）— 把原「C. Workspace 显式关闭」拆为 **C1 单 Workspace 关闭** 与 **C2 整个 ProjectSession / ProjectWindow 关闭** 两条独立路径；单工作区关闭不再误用 `ProjectSession.CloseAsync()`。
- **修订**: R4（2026-11-24）— C2 收敛为**唯一编排入口** `CloseProjectLifetimeAsync()`：正常命令与 `Window.Closed` 兜底汇聚到同一方法，先关全部 Binding 再关会话，并给出单次启动的幂等契约。
- **修订**: R5（2026-11-24）— 明确 C2 **关闭完成责任**：窗口路径从"`Closed` fire-and-forget"改为 **`Closing` 取消本次关闭 → 异步完成同一编排 → 再次关闭**；依据是应用停机模式 `OnLastWindowClose`（已核实），`ProjectWindow` 可能是最后一个窗口，`Closed` 之后的 fire-and-forget 会随消息循环退出而永远无法完成。
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`
- **相关**: `ADR-WP-30B-07`、`ADR-WP-30B-08`、`ADR-WP-30B-09`、`ADR-WP-30B-11`

## Context

现有代码已定义应用侧生命周期，但尚未接入视口：

- `ProjectSession.OpenWorkspaceAsync` 创建 `MapWorkspaceSession` 并加入 `_workspaces`
  （`ProjectSession.cs:144-180`）。
- **两种关闭是不同的方法，语义不可互换**（R3 已逐行核实）：
  - `CloseWorkspaceAsync(workspaceId)`：找到该工作区 → `workspace.CloseAsync()` → 从
    `_workspaces` 移除 → 返回 `bool`。**不关闭兄弟工作区、不关闭会话、不触碰 Asset Store /
    Database**（`ProjectSession.cs:182-202`）。
  - `CloseAsync()`：置 `Closing` → 循环关闭并移除**全部**子工作区 → `AssetStore.DisposeAsync()`
    → `Database.DisposeAsync()` → 置 `Closed` → 触发 `SessionClosed`
    （`ProjectSession.cs:204-253`）。
  - `DisposeAsync()` 只是 `CloseAsync()` 的等价入口，且会话已 `Closing`/`Closed` 时直接返回
    （`ProjectSession.cs:255-264`）。
- `MapWorkspaceSession` 构造时创建 `WorkspaceViewState`，`CloseAsync()` 置 `_closed` 并取消
  `_derivedWorkCts`，`DisposeAsync()` 释放该 CTS（`MapWorkspaceSession.cs:20-26,81-102`）。
  `CloseAsync` 与 `DisposeAsync` 是两件不同的事：前者取消派生工作，后者释放 CTS；顺序必须是
  先 `CloseAsync` 后 `DisposeAsync`。
- `ProjectWorkspaceViewModel.CloseWorkspaceAsync`（单工作区）依次为
  `Session.CloseWorkspaceAsync` → `Workspaces.Remove` → `workspace.DisposeAsync()`
  （`ProjectWorkspaceViewModel.cs:131-145`）；`CloseProjectAsync`（整项目）只调用
  `Session.CloseAsync()` 并触发 `ProjectClosed`（`ProjectWorkspaceViewModel.cs:150-156`）——
  R4 指出这个"先关会话、后关窗口"的顺序在接入视口后必须反转（见下）。
- WP-30A 交接要求：每个 `MapWorkspaceSession` 对应一个 `ViewportPresentationRuntime`，关闭时
  `DisposeAsync`（`IMPLEMENTATION_SUMMARY.md` §7）。
- `ViewportPresentationRuntime.DisposeAsync()` **先** `Coordinator.DisposeAsync()`（其中
  `CloseAsync()` 在发布器锁内置位 F-1 屏障），**后** `Publisher.Dispose()`；顺序即契约
  （`ViewportPresentationRuntime.cs:56-60`；`PresentationRequestCoordinator.cs:393-418`；
  `SNAPSHOT_LIFECYCLE.md` §5.1/§7）。

R1 引入 Desktop 内部具体类 `MapViewportBinding`，持有 Runtime、Renderer、generation 与
`volatile bool IsClosed`，是唯一的关闭入口。

**R2 修正的生命周期矛盾**：R1 文本把 `DetachedFromVisualTree` 当作关闭信号。Avalonia 12.1.6 的
`DetachedFromVisualTree` 只表示控件脱离 rooted visual tree（重新挂载、切换标签页、模板重建、
移出布局等**都可能**触发，具体取决于外壳实现），并不表示用户永久关闭视口。若在 Detach 时置
`IsClosed`，同一个控件重新挂载后将永远无法恢复绘制，且可能误关工作区。因此必须把
「暂时脱离」与「永久销毁」分开。

**R3 修正的关闭路径混淆**：R2 的第三种生命周期写成一条「Workspace 显式关闭」，并在末尾接
`ProjectSession.CloseAsync()`，这在语义上把**关闭单个工作区**与**关闭整个项目会话**合并成一条链。
按当前代码，二者是完全不同的操作：单工作区关闭走 `CloseWorkspaceAsync(workspaceId)`，兄弟工作区、
会话、Asset Store、Database 全部不受影响；`CloseAsync()` 才是项目级入口。若照 R2 的写法实现，
关闭一个标签页会连带关闭整个项目窗口。因此 R3 把 C 拆成 C1 / C2 两条路径，并明确各自不得到达
对方的方法。

**R3 核实的既有生命周期缺口（不虚构已完成的释放）**：`ProjectSession.CloseAsync()` 关闭子工作区时
只调用 `workspace.CloseAsync()`，**没有**调用 `workspace.DisposeAsync()`
（`ProjectSession.cs:223-239`）；`MapWorkspaceSession` 的 `_derivedWorkCts` 因此在整个项目关闭
路径上不会被释放。`ProjectSession.DisposeAsync()` 又只是 `CloseAsync()` 的等价入口，同样不会补上
这次 Dispose。本 ADR 如实记录该缺口为**既有生命周期待核实项**，不在本轮修复（本轮只改文档），
也不在文档中把它写成"已释放"。

**R4 修正的 C2 双入口问题**（已逐行核实当前调用链）：`ProjectWorkspaceViewModel.CloseProjectAsync`
当前是 `await Session.CloseAsync(ct)` → `ProjectClosed?.Invoke()`
（`ProjectWorkspaceViewModel.cs:150-156`）；`ProjectWindow` 用 `fresh.ProjectClosed += Close`
把该事件接到 `Window.Close()`（`ProjectWindow.axaml.cs:26-29`）。也就是说，今天的顺序是
**先关 `ProjectSession`（含 Asset Store / Database），再关窗口**，而 `ProjectWindow` 目前
**没有任何** `Closing` / `Closed` 处理器（`ProjectWindow.axaml.cs:9-40`）。

一旦接入视口，这个顺序就是错的：会话资源已释放时，Binding / Coordinator / Publisher 仍在活动。
同时，R3 只写了"`Window.Closed` 归 C2"，没说清楚它与"关闭项目命令"这两条入口如何不重复、
不互相绕过——`binding.CloseAsync()` 的幂等性只覆盖单个 Binding，**不能**替代"C2 整体只跑一次"。

因此 R4 在 ADR 层面定一个**唯一编排方法** `CloseProjectLifetimeAsync()`，两条入口都只调它；
不新增接口、事件总线、通用生命周期管理器或全局注册表。

**R5 修正的关闭完成责任缺口**（已核实真实代码与 Avalonia 12.1.6 反射）：
R4 把窗口路径定为"`Closed` 兜底、fire-and-forget 调用编排方法"，但没有回答**谁保证异步关闭流程
能完成**。核实结果否定了 fire-and-forget 模型：

- 应用使用 `StartWithClassicDesktopLifetime(args)` 启动（`Program.cs:39`），**全仓库无任何
  `ShutdownMode` 覆盖**（全源码 grep 零命中）；反射核实 Avalonia 12.1.6
  `ClassicDesktopStyleApplicationLifetime` 的默认 `ShutdownMode` 为 **`OnLastWindowClose`**，
  且存在 `HandleWindowClosed(Window)` —— 最后一个窗口 `Closed` 后立即触发停机、消息循环退出。
- `StartWindow` 在项目工作区打开后保留（多项目并存的 V1 语义，
  `StartWindow.axaml.cs:31-39`），但它**没有任何阻止先于 `ProjectWindow` 关闭的守卫**；
  `App.OnFrameworkInitializationCompleted` 也只设 `MainWindow`（`App.axaml.cs:24-29`）。
  因此用户可以先关启动面、再关项目窗口——**`ProjectWindow` 可能是最后一个窗口**。
- 一旦 `ProjectWindow` 是最后一个窗口：`Closed` 触发 → 生命周期立即停机 → 消息循环退出 →
  fire-and-forget 的 `CloseProjectLifetimeAsync()` 后续延续（派发在 UI 线程上）**永远不会执行**，
  编排死在第一个 await 之后。
- `Program.Main` 在消息循环退出后确有一个同步等待的停机兜底
  `registry.CloseAllAsync().GetAwaiter().GetResult()`（`Program.cs:41-45`；
  `ProjectSessionRegistry.cs:71-79`），但它是**进程级停机路径**，直接逐个调用
  `session.CloseAsync()`——绕过 C2 编排（不会先关 Binding），且它等待的是另一条路径，
  **不是** C2 任务本身；它不能作为方案 A 要求的"等待关闭任务完成的应用级所有者"。

结论：不存在能在 `ProjectWindow` 关闭后仍存活、且负责等待 C2 任务完成的所有者，方案 A
（应用生命周期保证等待）**不成立**；必须改为方案 B（关闭前异步完成 C2，见 §7.2 窗口入口）。

本 ADR 只规定对象之间"谁创建谁、谁持有谁、按什么顺序释放"，不实现接线。

## Decision Drivers

1. 保留 WP-30A 的每视口独立 runtime 与 F-1 关闭屏障，不改写 Publisher 语义。
2. 工作区（Application 会话）与视口（Desktop 控件）是不同生命周期，不能互相隐式销毁。
3. **视觉树挂载状态不能决定业务生命周期**：Detach 既不是销毁，也不是工作区关闭。
4. Binding / Runtime 必须有唯一的创建者与唯一的销毁入口，不依赖控件析构。
5. 暂时脱离后重新挂载必须能恢复绘制；永久销毁后必须创建新的 Binding / Runtime，绝不复用已关闭对象。
6. 不引入通用生命周期管理器、事件总线、全局注册表或跨视口协调服务。
7. 关闭顺序必须有明确依据；关闭保证的措辞不得暗示"零 post-close 绘制"。
8. **关闭单个 Workspace 与关闭整个 ProjectSession 是两条不同路径**：前者不得调用
   `ProjectSession.CloseAsync()`，后者必须先关闭其拥有的全部 Viewport Binding。
9. 只描述代码已有的释放行为；未完成的释放（如项目关闭时子工作区的 `DisposeAsync`）如实记为
   待核实项，不在文档中虚构。
10. **一条业务关闭路径只能有一个编排入口**：正常命令与窗口事件是同一件事的两个触发点，必须汇聚到
    同一个方法；否则两套清理逻辑必然产生"重复释放"或"漏关视口"。
11. **先 presentation、后 Application**：关闭顺序由依赖方向决定，不由触发点的调用顺序决定。

## Decision

### 1. 四种生命周期（不可混用）

| 事件 | 典型触发 | Binding / Runtime | Workspace | 绘制 |
|---|---|---|---|---|
| **A. Viewport 暂时脱离视觉树** | 可能由标签页切换、重新挂载（reparent）、模板重建、控件暂时移出布局等触发；具体外壳需实测 | **原样保留**，不置 `IsClosed`、不 Dispose | 不受影响 | 自然停止（不在树中，Avalonia 不再调用 `Render`）；重新挂载后恢复 |
| **B. Viewport 永久销毁** | 关闭该视口/标签；拥有者显式调用 `binding.CloseAsync()` | `IsClosed = true`（终态）；Coordinator → Publisher → Renderer 依次释放；Runtime 销毁 | **不自动关闭** | 关闭入口之后新进入的 `Render` 立即返回；已进入的在飞帧按 §5 边界处理 |
| **C1. 关闭单个 Workspace** | 单工作区的"关闭"命令（`ProjectWorkspaceViewModel.CloseWorkspaceAsync`）；该工作区的视口随之永久销毁 | 先执行 B（只关这一个视口） | `ProjectSession.CloseWorkspaceAsync(workspaceId)` 关闭并移除**该**工作区 → ViewModel 从 `Workspaces` 移除 → `workspace.DisposeAsync()`。**不得**调用 `ProjectSession.CloseAsync()` | 同 B |
| **C2. 关闭整个 ProjectSession / ProjectWindow** | **唯一编排入口** `CloseProjectLifetimeAsync()`，只由两个触发点调用：①"关闭项目"命令（`CloseProjectAsync`）；②项目窗口关闭流程（`Closing` 取消-编排-重关，R5） | 先关闭该会话下**所有**仍存活的 Binding（各自走 B） | 全部 Binding 关闭后调用 `ProjectSession.CloseAsync()`：关闭全部子工作区 → `AssetStore` → `Database` | 同 B |

**判定规则**：

1. `DetachedFromVisualTree` **只**对应 A，永远不触发 B / C1 / C2。
2. B 由视口宿主拥有者的显式命令决定；控件自身没有权限销毁 Binding。
3. **C1 ≠ C2**：C1 的作用域是一个工作区（外加它的视口），C2 的作用域是整个会话（含全部工作区与
   包级资源）。C1 不得调用 `ProjectSession.CloseAsync()`，C2 不得只关一个 Binding 就调用
   `ProjectSession.CloseAsync()`（必须先关全部 Binding）。
4. `Window.Closed` / `Window.Closing` 均归属 **C2**，不归属 C1，也不归属"单个 Workspace Tab
   关闭"。窗口关闭的**编排接入点**是 `Closing`（R5 取消-编排-重关）；`Closed` 是编排完成后的
   结果事件。若某个窗口确实只承载一个 Workspace，仍按该窗口的真实宿主所有权描述（该特例不得
   推广为通用规则）。
5. **C2 的两个触发点不是两条路径**：`CloseProjectAsync` 与窗口关闭（`Closing` 接入）都只调用
   `CloseProjectLifetimeAsync()`，清理逻辑只有一份（§7.2）。任何"在 `Closing` / `Closed` 里
   再写一遍释放"的做法都被拒绝（见 Rejected Alternatives）。

### 2. Avalonia 12.1.6 生命周期事实（已核实）

| API | 12.1.6 语义 | 证据 |
|---|---|---|
| `Visual.AttachedToVisualTree` | "Raised when the control is attached to a **rooted visual tree**"；同一控件可多次触发 | `avalonia/12.1.6/lib/net10.0/Avalonia.Base.xml` `E:Avalonia.Visual.AttachedToVisualTree` |
| `Visual.DetachedFromVisualTree` | "Raised when the control is detached from a **rooted visual tree**"；未声明"永久"或"窗口关闭"，可再次 Attach | 同上 `E:Avalonia.Visual.DetachedFromVisualTree` |
| `Window.Closing` | "Fired **before** a window is closed"；`WindowClosingEventArgs` 继承 `System.ComponentModel.CancelEventArgs`，可用 `Cancel` 否决关闭；另有 `IsProgrammatic` / `CloseReason` | `Avalonia.Controls.xml` `E:Avalonia.Controls.Window.Closing`、`T:Avalonia.Controls.WindowClosingEventArgs`；反射确认基类与 `Cancel` 属性 |
| `Window.Closed` | 窗口关闭完成后触发（`EventHandler`，无取消参数） | `Avalonia.Controls.xml` `E:Avalonia.Controls.Window.Closed`；反射确认事件签名 |
| `Visual.IsAttachedToVisualTree` | 12.1.6 中为 **internal**，不作为公开契约使用 | 反射：`Avalonia.Visual` 上属性 `public=False` |
| `ClassicDesktopStyleApplicationLifetime.ShutdownMode` | 默认值为 **`OnLastWindowClose`**（枚举序号 0；另两个值 `OnMainWindowClose` / `OnExplicitShutdown`）；`HandleWindowClosed(Window)` 在窗口数归零时触发停机 | R5 反射核实（Avalonia 12.1.6）：新建 lifetime 实例读取 `ShutdownMode` 属性值为 `OnLastWindowClose`；反射列出 `HandleWindowClosed(Window)` / `Shutdown(Int32)` / `TryShutdown(Int32)` 方法 |

**结论**：不能把 `DetachedFromVisualTree` 当作永久关闭；也不能在 `Window.Closing` 里执行不可逆
销毁——其他处理器可能随后 `Cancel`。可靠的窗口级销毁点是 `Window.Closed`（已不可取消）。
`Window.Closing` 不做任何资源释放；但它可以承担 R5 定义的**取消本次关闭 + 接入异步编排 +
完成后再次关闭**的流程控制（这不是销毁，见 §7.2）。

### 3. 暂时脱离与重新挂载（A 的具体方案）

**本 ADR 明确选择保留 Binding 与 Runtime**，不创建新对象、不改 `IsClosed`；不存在"Detach 时释放
Runtime"的分支（那等同于 §4 的永久销毁，必须由拥有者显式决定）。

1. **Detach 时**：控件置自身 `_isAttached = false`（仅控制是否入队失效），**不**调用
   `binding.RequestClose()`、不 Dispose Runtime、不关闭 Workspace、不停止已在活动的请求。
   已提交的请求可以继续完成并发布；此时没有控件绘制，发布是无害的。
2. **Attach 时**：控件置 `_isAttached = true`；若 `binding.IsClosed` 为 true，则**拒绝恢复绘制**
   并记录诊断——这是终态，拥有者必须创建新的 Binding 与控件，不能复用旧对象。
3. **正常恢复**：Attach 时不假定 `Bounds` 已有效。若当前尺寸 > 0，则 `binding.OnViewportChanged`
   推进 generation 并重发一次请求（重新挂载视为呈现状态变更，避免显示 Detach 期间的陈旧快照）；
   若尺寸仍为 0，则等待首个有效 `SizeChanged` 再请求，避免以 0 尺寸发起请求。随后
   `InvalidateVisual()`。
4. **下一帧**：`Render` 调用 `binding.CaptureFrame()`，读取 `Publisher.Current` 的**最新**快照与
   当前相机值；无需重放 Detach 期间的中间帧。
5. **不引入**生命周期管理器/事件总线：恢复只依赖控件自身的 `AttachedToVisualTree` 与
   `Render`，以及 binding 的两个方法。

**不变量**：一个控件实例只能绑定一个未关闭的 Binding；同一 Workspace 在 M1 至多有一个未关闭
Binding（工厂负责断言）；Binding 一旦 `IsClosed` 即为终态，不可"重开"。永久销毁必须同时丢弃
控件；新视口由工厂创建新的 Binding + Runtime。**临时 Detach 路径不会产生已关闭 Binding**；
若 Attach 时发现已关闭，说明拥有者在永久销毁后错误复用了控件，必须 fail-fast 并新建，
不得静默留下"无法恢复绘制"的状态。

### 4. 视口永久销毁（B 的具体方案）

- **谁判定**：Binding 的拥有者（视口宿主：窗口/工作区视图层），不是控件、不是可视树事件。
- **触发命令**：
  - 关闭单个视口/标签的显式命令（只关视口、保留工作区）；
  - C1 / C2 的前置步骤：两条工作区关闭路径都先执行 B；
  - 承载窗口的关闭不属于 B 的独立触发点，它归 C2，由 C2 统一先关全部 Binding。
- **顺序**：`binding.CloseAsync()`（内部**先**置 `IsClosed`，再 Coordinator → Publisher →
  Renderer）→ 控件停止失效并丢弃引用。置标志先于任何释放，与 §7 一致。
- **Workspace 保留**：B 本身不调用 `ProjectSession.CloseWorkspaceAsync`，也**不调用**
  `ProjectSession.CloseAsync`；工作区继续存活，可由新视口重新挂载（新 Binding + 新 Runtime）。
  工作区是否结束由 C1 / C2 决定，不由 B 决定。
- **不可复用**：已关闭 Binding 不得再次 Attach；拥有者若需重新显示，必须走工厂创建。

### 5. 关闭保证的准确边界（B / C1 / C2 共用）

允许的时序：

```text
Render enters
  ↓
Reads IsClosed == false
  ↓
Close sets IsClosed = true
  ↓
Close returns
  ↓
Already-entered Render may continue
```

准确保证：

1. `Render` 在入口观察到 `IsClosed == true` 时立即返回：不获取 lease、不创建 Skia 资源、不发绘制命令。
2. 在关闭置位前已经观察到 `IsClosed == false` 的在飞帧，允许自然完成，可能继续创建资源并发命令；它只使用已捕获帧输入、局部 Skia 对象与 M1 无状态 Renderer，不读已释放的 Runtime / Publisher。
3. **不保证**关闭命令返回后绝对没有绘制调用——在飞帧可能仍在执行。
4. **不保证**中断已经进入的回调；Avalonia 12.1.6 未公开取消契约。
5. 不等待在飞帧结束。
6. UI 线程不等待渲染完成。
7. 不引入锁、信号量、帧屏障或独立 Render Thread。

### 6. 对象创建 / 持有 / 释放总表

| 对象 | 创建者（唯一） | 持有者 / 所有者 | 销毁入口（唯一） | 依据 |
|---|---|---|---|---|
| `ProjectSession` | `OpenProjectUseCase`（组合根注册） | `ProjectSessionRegistry` + `ProjectWorkspaceViewModel` | **仅 C2 的编排方法**（`CloseProjectLifetimeAsync`）在关闭全部 Binding **之后**调用 `CloseAsync` / `DisposeAsync`。注意：`Program.Main` 的进程级停机兜底（`registry.CloseAllAsync()`）**不经**该编排，直接逐个 `session.CloseAsync()`（R5 如实记录，见 §8B 第 30 条）。C1 **不得**调用 | `Program.cs:37-45`；`ProjectSession.cs:204-264` |
| `MapWorkspaceSession` | `ProjectSession.OpenWorkspaceAsync` | `ProjectSession._workspaces` + `ViewModel.Workspaces` | **C1**：`ProjectSession.CloseWorkspaceAsync(workspaceId)`（内部 `CloseAsync`）→ `Workspaces.Remove` → `workspace.DisposeAsync()`；**C2**：`ProjectSession.CloseAsync()` 内部逐个 `CloseAsync`，**不调用** `DisposeAsync`（缺口见 Open Questions 第 6 条） | `ProjectSession.cs:182-202,223-239`；`ProjectWorkspaceViewModel.cs:131-145` |
| `WorkspaceViewState` | `MapWorkspaceSession` 构造函数 | `MapWorkspaceSession.ViewState` | 随工作区结束（无独立 Dispose） | `MapWorkspaceSession.cs:25,38,109-125` |
| `MapViewportBinding` | Desktop 组合根的 workspace 工厂（单一工厂方法） | **视口宿主拥有者**（工作区视图层）；控件只持一个引用；项目级维护一个仅用于枚举的存活引用集合（不拥有关闭权） | 拥有者调用 `binding.CloseAsync()`（单一入口）：C1 关闭该工作区的一个；C2 由 `CloseProjectLifetimeAsync()` 遍历全部 | ADR-07 R1/R2/R3；本 ADR §3/§4/§7 |
| **C2 编排**（`CloseProjectLifetimeAsync`，`ProjectWorkspaceViewModel` 上的方法） | `ProjectWorkspaceViewModel` 自身（实例方法，无独立对象、无接口） | `ProjectWorkspaceViewModel` | 不单独销毁；其幂等状态随 ViewModel 一起消亡 | 本 ADR §7.2；`ProjectWorkspaceViewModel.cs:150-156` |
| `ViewportPresentationRuntime` | `MapViewportBinding` 构造函数（`ViewportPresentationRuntime.Create()`） | `MapViewportBinding` 私有字段 | `binding.CloseAsync()` 内 `DisposeAsync()` | `IMPLEMENTATION_SUMMARY.md` §7；`ViewportPresentationRuntime.cs:30-35` |
| `Renderer`（Desktop 内部绘制对象） | `MapViewportBinding` 构造函数，`internal sealed` | `MapViewportBinding` 私有字段 | `binding.CloseAsync()` 内、Publisher 释放后 `Dispose()` | ADR-07；ADR-09 §8/§9；本 ADR §7 |
| `MapViewportControl` | Desktop 窗口/视图层（Binding 已存在后创建） | 所在 `Window` 的可视树 | 随可视树/窗口销毁；**Detach 不销毁、不关闭** | ADR-07；Avalonia 12.1.6 `Visual.*VisualTree` |
| `Camera`（M1 无独立类型） | 数值由 `WorkspaceViewState` 初始化 | `WorkspaceViewState`（Application 瞬态） | 随工作区；视口只持有不可变复制值 | ADR-07 问题 3；ADR-11 |
| Skia 资源 | draw 回调内（除 Avalonia 借出的 canvas/surface） | 本次回调 | 本次回调 `using` | ADR-09 |

**唯一性说明**：

- Binding 只由工厂方法创建；Runtime 与 Renderer 只由该 Binding 创建并销毁；控件从不创建或销毁三者。
- M1 中 Binding 与 Workspace 是 1:1 且**同属一个拥有者**（工作区视图层）。因此 C1 只需要关闭
  该工作区对应的那一个 Binding；C2 必须遍历该会话下**全部**存活 Binding 并逐个 `CloseAsync()`，
  再调用 `ProjectSession.CloseAsync()`。
- `Window.Closing` / `Window.Closed` 是**事件**而非销毁者：`Closing` 只承担"取消本次关闭 +
  接入编排 + 完成后再次关闭"的流程控制（R5），`Closed` 只反映关闭结果；二者都不得自带清理逻辑，
  释放动作仍然只发生在 `CloseProjectLifetimeAsync()`（内部仍是逐个 `binding.CloseAsync()`）。
- C2 编排方法本身是 `ProjectWorkspaceViewModel` 上的实例方法，不是新类型、不是接口、不是服务；
  它的幂等状态可以用一个私有字段表达，不需要 DI、注册表或调度器。
- **存活 Binding 集合是"观察"而非"所有权"**：项目级只保留一个用于 C2 枚举的引用集合
  （M1 与 `Workspaces` 1:1，可用 `Dictionary<Guid, MapViewportBinding>` 或等价具体集合），
  它不负责创建、不负责释放；真正的创建者仍是工厂，释放者仍是 `binding.CloseAsync()`。
- 任何路径都不得跳过 Binding 直接调用 `ProjectSession.CloseAsync()`：那会在 presentation 管线
  仍活动时释放包级资源。

### 7. 关闭顺序（C1 / C2 两条独立路径）

R2 曾把两者写成一条链，末段接 `ProjectSession.CloseAsync()`，导致"关一个标签页 = 关整个项目"。
以下两条路径互不包含，且都**不**由控件析构或 `DetachedFromVisualTree` 触发；窗口路径由
`Window.Closing` 的流程控制接入（R5，见 §7.2 窗口入口）。

#### 7.1 C1 — 关闭单个 Workspace

```text
0. 触发：单工作区"关闭"命令（ProjectWorkspaceViewModel.CloseWorkspaceAsync）
1. 拥有者调用该工作区对应的 binding.CloseAsync()
     1a. binding.IsClosed = true（volatile）
     1b. Coordinator.CloseAsync()：停新请求、取消活动请求，在 publisher gate 内置位 F-1 屏障
     1c. Publisher.Dispose()：拒绝一切发布与版本分配；Current 不清空
     1d. Renderer.Dispose()（M1 幂等空操作）
2. ProjectSession.CloseWorkspaceAsync(workspaceId)
     → MapWorkspaceSession.CloseAsync()：取消 _derivedWorkCts
     → 从 ProjectSession._workspaces 移除该工作区
3. ProjectWorkspaceViewModel：Workspaces.Remove(workspace)
4. workspace.DisposeAsync()：释放 _derivedWorkCts
```

**作用域边界（硬约束）**：

- **不得**调用 `ProjectSession.CloseAsync()`；单工作区关闭不结束会话。
- **不得**关闭兄弟工作区或其 Binding；每个工作区只关自己的那一个。
- **不得**释放 `AssetStore` / `Database`；它们归会话所有。
- 步骤 2 的 `CloseWorkspaceAsync` 返回 `bool`；返回 `false`（该 id 不在集合中）不是错误，可视为
  已关闭/未打开，仍应继续步骤 3–4 的本地清理。
- 步骤 2 与 3 的先后不可交换：`Workspaces.Remove` 在 `CloseWorkspaceAsync` 之后，与现有代码一致。

#### 7.2 C2 — 关闭整个 ProjectSession / ProjectWindow

**唯一编排入口**：`ProjectWorkspaceViewModel.CloseProjectLifetimeAsync()`。它是 C2 唯一的业务关闭
方法，不是新类型、不是接口、不是服务、不注册 DI。两个触发点都只调它一次：

| 入口 | 触发时机 | 行为 |
|---|---|---|
| **正常入口** `CloseProjectAsync()`（命令） | 用户点"关闭项目" | **只** `await CloseProjectLifetimeAsync()`；方法内部完成"关 Binding → 关会话 → 通知窗口"。**不得**保留现有"先 `Session.CloseAsync()` 再 `ProjectClosed`"的顺序 |
| **窗口入口** `Window.Closing`（R5：取消-编排-重关） | 用户点 X、系统关闭、任何 `Window.Close()` 请求 | 见下方"窗口关闭流程（R5）"；处理器的职责是**取消本次立即关闭、接入同一编排、等待其完成后再关闭窗口**。不复制任何清理逻辑 |

#### 窗口关闭流程（R5：取消 → 异步编排 → 再次关闭）

R5 已核实（Context）：`ProjectWindow` 可能是最后一个窗口；此时若在 `Closed` 里 fire-and-forget，
停机（默认 `ShutdownMode.OnLastWindowClose`）会让编排的延续永远不执行。因此窗口关闭的接入点
从 `Closed` 前移到 `Closing`，用"取消本次关闭"换取应用继续存活：

```text
用户请求关闭 ProjectWindow（点 X / 系统关闭 / Window.Close()）
  ↓
Closing 处理器判断 C2 编排状态：
  a) 已完成（例如"关闭项目"命令已跑完整个编排）→ 不取消，放行关闭（不触碰资源）
  b) 未启动 → 取消本次关闭（Cancel = true），启动 CloseProjectLifetimeAsync()
  c) 进行中 → 取消本次关闭（Cancel = true），等待同一个进行中的编排（幂等契约 §7.2）
  ↓ (b/c)
await 编排完成（异常就地观察：记诊断，不留下未观察异常）
  ↓
再次请求 Window.Close() → Closing 再次触发 → 走分支 a → 放行
  ↓
Closed 触发 → 窗口真正关闭；若这是最后一个窗口，停机此时才发生，而 C2 已完成
```

流程裁决：

1. **第一次 `Closing` 中不释放任何资源**：只做"取消 + 启动/接入编排"。资源释放只发生在编排
   方法内部，与命令路径完全同一条代码路径。
2. **重复关闭请求只执行同一套 C2**：编排进行中再次收到关闭请求（用户连点 X），每次都进入
   分支 c，等待同一个操作；C2 整体最多启动一次的幂等契约不变。
3. **完成责任归属明确**：
   - **命令路径**：`CloseProjectAsync` 的调用方 `await` 编排完成——完成由命令调用方等待。
   - **窗口路径**：`Closing` 处理器 `await` 编排完成后再关闭窗口——完成由处理器等待，且
     窗口在编排完成前**保持打开**，这保证了应用不会在 C2 结束前停机。
   - 不存在第三种无人等待的情形；`Closed` 处理器**不承担**编排接入职责（本 ADR 不要求为
     `Closed` 新增接线；若实现期保留了 `Closed` 兜底调用，它只能是幂等契约下的重复调用，
     不得承担完成责任）。
4. **失败处理**：编排抛出异常时，处理器就地捕获、记诊断并**仍然放行窗口关闭**——不允许
   出现"窗口永远关不掉"或"未观察异常"。这与既有裁决一致（单个 Binding 关闭失败不终止编排）；
   编排内部已保证"尽可能走到 `ProjectSession.CloseAsync()`"，残余状态由诊断暴露。
5. **取消-重关与 `ProjectClosed` 的收尾重叠**：编排步骤 4 触发 `ProjectClosed` → `Window.Close()`
   与处理器的"再次关闭"可能指向同一次关闭；行为要求是两者都只导致窗口被关闭一次（分支 a
   保证 `Closing` 在编排完成后直接放行，重复 `Close()` 无额外副作用）。接线时的防重入细节
   见 Open Questions 第 7 条。
6. **停机兜底的位置不变**：`Program.Main` 消息循环退出后的
   `registry.CloseAllAsync().GetAwaiter().GetResult()`（`Program.cs:41-45`）是**进程级停机
   兜底**，不是 C2 的一部分：它直接逐个调用 `session.CloseAsync()`，不先关 Binding。在 R5
   模型下正常路径中 C2 已在窗口关闭前完成，该兜底只剩异常退出（崩溃、其他来源的进程终止）
   场景；其"绕过编排"的边界如实记录（§8B 第 30 条），不据此改写它。

编排步骤（按依赖方向，不可重排）：

```text
CloseProjectLifetimeAsync()
0. 单次启动保证：若已启动过，直接等待/返回到那次操作，不重复执行（见 §7.2 幂等契约）
1. 关闭该 ProjectSession 下所有仍存活的 Viewport Binding
     对每个 Binding 执行与 §7.1 步骤 1（1a–1d）完全相同的子序
     —— IsClosed → Coordinator（F-1 屏障）→ Publisher → Renderer
     —— 相同顺序、相同屏障、相同幂等要求；串行、不跳过、不并行
2. 全部 Binding 关闭完成后，调用 ProjectSession.CloseAsync()
     2a. 置 Closing：拒绝新工作区
     2b. 循环关闭全部子工作区（MapWorkspaceSession.CloseAsync，逐个从集合移除）
     2c. AssetStore.DisposeAsync()
     2d. Database.DisposeAsync()
     2e. 置 Closed → 触发 SessionClosed
3. ViewModel 视图侧清理：清空存活 Binding 集合与 Workspaces 集合
     （`ProjectSession.CloseAsync` 只清 Application 的 `_workspaces`，不会清 ViewModel 的
       ObservableCollection；该集合在窗口销毁前必须显式清空，避免悬空引用）
4. 通知窗口：触发 `ProjectClosed` 一次 → `Window.Close()`。窗口路径下本次同步关闭已被 `Closing`
   取消、窗口仍打开，因此该调用就是"编排完成后再次关闭"的落地：`Closing` 见编排已完成，放行。
5. 防御分支：若步骤 4 时窗口因任何异常场景已经关闭（`Closed` 已触发），跳过关闭调用，只保留
   事件不重复触发；此时按幂等契约，任何后续入口调用都只是等待/吸收这次已完成的编排。
```

**幂等契约（行为级，不规定实现类型）**

1. **C2 最多启动一次**：第一次调用建立"已启动"状态，之后的调用一律视为同一次操作。
2. **重复调用不得重复关闭**：不得再次遍历 Binding、不得再次调用 `ProjectSession.CloseAsync()`、
   不得再次释放 `AssetStore` / `Database`。
3. **不得绕过进行中的第一次调用**：若第一次尚未完成，后续调用必须**等待它**，而不是并行跑一遍，
   也不是直接返回"已完成"。
4. **正常入口与窗口关闭连续触发时，最终只执行一套 C2 顺序**：两者汇聚到同一方法、同一状态；
   窗口路径的关闭请求在编排进行中只会等待（`Closing` 分支 c），不会另起一遍。
5. **`binding.CloseAsync()` 的幂等性不能替代 C2 的幂等性**：前者只保证单个视口重复关闭无害，
   不能阻止第二次调用再去跑 `ProjectSession.CloseAsync()` 或再释放包级资源。
6. **一旦启动就跑到结束**：命令的 `CancellationToken` 只用于启动前的拒绝（例如会话尚未 Ready），
   不用于中途放弃。中途取消会留下"Binding 已关、会话未关"的半关闭状态，因此启动后以
   `CancellationToken.None` 继续。
7. 实现只需要一个私有字段记忆首次调用的完成状态（或等价的最小手段）；不引入锁服务、
   调度器、事件总线或通用生命周期管理器。

**作用域边界（硬约束）**：

- 步骤 1 必须先于步骤 2：在 presentation 管线仍活动时释放 `AssetStore` / `Database` 是无效顺序。
- 步骤 1 必须覆盖**全部**存活 Binding，不能只关当前选中的那一个；遍历依据是该会话的存活
  Binding 集合（M1 与 `Workspaces` 1:1），不是 `SelectedWorkspace`。
- 步骤 2b 当前**只**调用 `CloseAsync()`，**不**调用子工作区的 `DisposeAsync()`（见 Open Questions 第 6 条）；
  本文档不声称这些 CTS 已被释放。
- `Window.Closed` 归 C2，窗口关闭的编排接入点是 `Closing`（R5）；若某窗口只承载一个 Workspace，
  按其真实宿主所有权仍走 C2 的"先关全部 Binding 再关会话"，不得因此把该特例当作 C1 的通用规则。
- **禁止**在 `Closing` / `Closed` 处理器里直接调用 `Session.CloseAsync()` 或自行 Dispose
  Binding；那会绕开步骤 1 的顺序，并制造第二个销毁入口。`Closing` 只做流程控制（取消、接入、
  重关），`Closed` 不承担编排职责。
- **单个 Binding 关闭异常不终止编排**：记诊断后继续关闭其余 Binding，并仍然走到
  `ProjectSession.CloseAsync()`；否则会留下"部分视口已关、会话仍 Ready"的半关闭状态，比直接
  完成关闭更危险。
- **Binding 不依赖控件存活**：按 ADR-07，引用方向是单向的（控件 → Binding），Binding 不持有
  控件、不创建控件。因此编排执行期间（含窗口路径下窗口仍打开，以及防御分支下窗口已关闭、
  可视树已拆的场景），从 ViewModel 持有的集合里逐个关闭 Binding 都是安全的；已关闭窗口的控件
  不可能再发起 `Render`，此时关闭只剩"停请求 + 屏障 + 释放"。

#### 7.3 两条路径的共同依据

- **先置标志、后释放**：步骤 1a 先于任何释放，新进入的 `Render` 在获取 lease 前返回；但不承诺
  在飞帧被中断（§5）。
- **Coordinator 先于 Publisher**：`1b → 1c` 是 WP-30A / Phase 1 的既有契约（F-1 屏障必须在
  publisher gate 内、且在 `Publisher.Dispose()` 之前置位）。
- **Renderer 在 Publisher 之后**：`1c → 1d`，因为 M1 Renderer 无需要抢先释放的 Skia 资源（ADR-09）。
- **presentation 先于 Application**：Binding 关闭先于 `MapWorkspaceSession.CloseAsync` /
  `ProjectSession.CloseAsync`。Coordinator 独立完成取消与迟到拒绝，不依赖工作区的
  `_derivedWorkCts`"顺带"保证正确性。
- **Application 内部按现有逆序**：子工作区先于 `AssetStore` / `Database`，与
  `ProjectSession.CloseAsync` 现有实现一致。
- **顺序由依赖方向决定，不由触发点决定**：现有 `CloseProjectAsync` 是
  `Session.CloseAsync()` → `ProjectClosed` → `Window.Close()`，即"先 Application、后窗口"。
  R4 要求在它前面插入"先关闭全部 Binding"，因此接线时必须改写该命令体——这是本轮唯一
  被**要求改变**的既有调用顺序，且只发生在 Desktop 的 ViewModel 层（不改 Application 代码）。
- **窗口关闭是编排完成的结果，不是第一步**：`ProjectClosed` → `Window.Close()` 仍然保留为收尾
  通知；R5 之后窗口路径的关闭请求在编排完成前被 `Closing` 取消（§7.2 窗口关闭流程），编排完成
  后的关闭请求直接放行。窗口关闭触发的任何入口调用都是幂等契约下的重复吸收（§7.2 幂等契约）。
- **关闭期间不再接受新视口**：步骤 2a 置 `Closing` 后 `OpenWorkspaceAsync` 会拒绝新工作区；
  步骤 1 与 2 之间不允许再创建 Binding（组合根工厂在会话非 Ready 时拒绝创建）。

### 8. 十个必答题

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 谁创建 Workspace | `ProjectSession.OpenWorkspaceAsync`（Application），经 Core Domain 解析上下文后构造；Desktop 不自行 new。 |
| 2 | 谁创建 MapViewportControl | Desktop 窗口/视图层，在 Workspace 与 Binding 均已存在后创建；构造/Attach 时接收 Binding。控件不创建 Binding。 |
| 3 | 谁创建 Renderer | `MapViewportBinding` 构造函数创建 `internal sealed` 绘制对象作为私有字段；不注册 DI、不暴露接口、不归控件持有。 |
| 4 | Workspace 和 Viewport 是什么关系 | 组合关系：Viewport 是 Workspace 的可视宿主；Binding 引用的上下文与 ViewState 来自 Workspace，Workspace 不依赖 Viewport 的存在（会话可无视图存活）。M1 为 1:1。 |
| 5 | 一个 Workspace 是否允许多个 Viewport | **M1 不允许**（相机/工具状态仍在工作区级）；将来拆分每视口状态后可重新裁决。多个工作区/窗口仍可并存且 runtime 彼此隔离。 |
| 6 | Viewport 销毁是否意味着 Workspace 销毁 | **否**。暂时 Detach 什么都不销毁；永久销毁只关闭该视口的 presentation（Binding/Runtime/Renderer），Workspace 仍归 Application 生命周期。Workspace 只在 **C1**（`CloseWorkspaceAsync`）或 **C2**（`CloseAsync`）中结束。 |
| 7 | Workspace 关闭时如何处理未完成 Presentation Request | `Coordinator.CloseAsync()` 停新请求、取消活动请求、在 publisher 锁内置屏障；其后任何完成路径（包括发布器未释放窗口内的迟到完成）一律 `RejectedClosed`。workspace 的 `_derivedWorkCts` 取消是额外一层，不承担正确性。C1 与 C2 都走这一段，无分支。 |
| 8 | Renderer 释放早于或晚于 Publisher | M1 Renderer 无资源，`Dispose()` 幂等空操作；由 Binding 在 Publisher 释放**之后**调用（步骤 3→4）。不允许把 Skia 资源放进 Renderer 改变这一顺序（ADR-09）。 |
| 9 | 如何保证关闭之后没有迟到绘制 | 精确边界见 §5：入口读到 `IsClosed == true` 立即返回；此前已读到 `false` 的在飞帧允许完成；不保证关闭返回后零绘制调用、不保证中断在飞回调、不等待。另由 F-1 屏障拒绝关闭后的发布。 |
| 10 | 如何保证绘制过程中 ViewState 一致 | 只有 UI 线程写 `WorkspaceViewState`（ADR-08）；控件 `Render` 调用 `binding.CaptureFrame()`，由 Binding 同步复制成不可变帧输入（ADR-11）；Renderer 与 draw operation 不回读可变对象或控件；快照由 publisher 原子交换。 |

### 8A. 关闭路径补充裁决（R3）

| # | 问题 | 裁决 |
|---|---|---|
| 11 | 关闭单个 Workspace 是否可以调用 `ProjectSession.CloseAsync()` | **绝对不可以**。该调用会关闭全部兄弟工作区并释放 `AssetStore` / `Database`，等于关掉整个项目。C1 只允许 `CloseWorkspaceAsync(workspaceId)`。 |
| 12 | 关闭整个 ProjectSession 是否可以只关当前选中的 Binding | **不可以**。必须先遍历关闭该会话下全部存活 Binding，再调用 `CloseAsync()`；否则 presentation 管线仍活动时包级资源已释放。 |
| 13 | `Window.Closed` / `Window.Closing` 应归入哪条路径 | **C2**。整个项目窗口的关闭，不是单个 Workspace Tab 的关闭。`Closing` 不承担任何**销毁**（可被 `Cancel`），但按 R5 承担"取消本次关闭 + 接入编排 + 完成后再次关闭"的流程控制（§7.2）；`Closed` 不承担编排接入职责。 |
| 14 | 单工作区关闭后，兄弟工作区 / 会话 / Asset Store / Database 是否受影响 | **均不受影响**。`CloseWorkspaceAsync` 只操作目标工作区与其自身集合项（`ProjectSession.cs:182-202`）。 |
| 15 | 项目关闭时子工作区的 `DisposeAsync()` 是否被调用 | **当前没有被调用**。`CloseAsync` 只调 `CloseAsync()`（`ProjectSession.cs:223-239`），`ProjectSession.DisposeAsync` 也不补。记为既有缺口（Open Questions 第 6 条），本轮不改代码、不写成已完成。 |
| 16 | 两条路径中 Binding 的关闭子序是否相同 | **相同**：`IsClosed` → Coordinator（F-1 屏障）→ Publisher → Renderer。C1 执行一次，C2 对每个 Binding 各执行一次；无第二套顺序。 |
| 17 | 一个 Workspace 对应几个 Binding（M1） | 一个。Binding 与 Workspace 1:1 且同属一个拥有者，因此 C1 无需遍历、C2 需要遍历。 |

### 8B. C2 编排补充裁决（R4）

| # | 问题 | 裁决 |
|---|---|---|
| 18 | C2 是否有唯一业务关闭入口 | **有**：`ProjectWorkspaceViewModel.CloseProjectLifetimeAsync()`。它是实例方法，不新增类型/接口/服务。除它以外，任何代码不得自行编排"关 Binding + 关会话"的组合。 |
| 19 | `CloseProjectAsync` 与窗口关闭是什么关系 | **同一个方法的两个触发点**，不是两条路径。正常入口只 `await CloseProjectLifetimeAsync()`；窗口入口由 `Closing` 取消本次关闭后接入同一方法并等待完成（R5）。禁止在 `Closing` / `Closed` 里再实现一套清理。 |
| 20 | 当前"先 `Session.CloseAsync()` 再 `ProjectClosed`"能否保留 | **不能**。必须改为"关全部 Binding → `ProjectSession.CloseAsync()` → 清理视图集合 → 通知窗口"。旧顺序会在 presentation 仍活动时释放包级资源。 |
| 21 | C2 的幂等如何表达 | 行为契约：最多启动一次；重复调用等待首次操作而非并行重跑；不重复关会话、不重复释放资源。实现只需一个私有字段记忆首次调用，不引入锁服务或调度器。 |
| 22 | `binding.CloseAsync()` 幂等是否足以保证 C2 幂等 | **不足**。单个 Binding 重复关闭无害，但无法阻止第二次调用继续执行 `ProjectSession.CloseAsync()` 与包级资源释放；C2 必须有自己的"已启动"状态。 |
| 23 | 编排进行中再次触发窗口关闭怎么办 | `Closing` 取消本次同步关闭（分支 c），等待**同一个**进行中的编排，完成后再次请求关闭；不得并行执行、不得跳过等待后自行返回。 |
| 24 | 项目关闭命令被取消（`CancellationToken`）时能否中途放弃 | **不能**。令牌只用于启动前的拒绝；一旦开始关闭 Binding，就以 `CancellationToken.None` 跑到结束，否则会留下"Binding 已关、会话未关"的半关闭状态。 |
| 25 | 存活 Binding 集合由谁维护、是否算注册表 | 由项目级 ViewModel 维护一个**仅用于枚举**的具体集合（M1 与 `Workspaces` 1:1）。它不是全局注册表、不是 DI、不拥有释放权；创建仍归工厂，释放仍归 `binding.CloseAsync()`。 |
| 26 | **（R5）谁保证 C2 异步流程能够完成** | **命令路径**由 `CloseProjectAsync` 的调用方 `await`；**窗口路径**由 `Closing` 处理器 `await`——它先取消本次同步关闭，使窗口（进而应用）在编排完成前保持存活，编排完成后才再次关闭窗口。不存在无人等待的情形。 |
| 27 | **（R5）`ProjectWindow` 是否可能是最后一个窗口** | **可能**。`StartWindow` 保留但无阻止先关的守卫（`StartWindow.axaml.cs:31-39`）；默认 `ShutdownMode.OnLastWindowClose`（反射核实）下，最后一个窗口 `Closed` 即停机。R4 的"`Closed` fire-and-forget"在该场景下编排延续永不执行，必须用 `Closing` 取消-重关模型规避。 |
| 28 | **（R5）在 `Closing` 取消并接入编排是否违反"`Closing` 不销毁"裁决** | **否**。被拒绝的是在 `Closing` 中**同步执行不可逆销毁**（其他处理器可能随后 Cancel）。R5 的 `Closing` 只做流程控制：不释放任何资源，销毁仍只发生在编排方法内部；"先取消、异步完成后重关"恰恰避免了"已销毁但窗口仍开"的状态。 |
| 29 | **（R5）编排失败时窗口是否会被卡死** | **否**。处理器就地观察异常、记诊断后仍放行窗口关闭；不留未观察异常，也不留关不掉的窗口。残余会话状态由诊断暴露；若此后进程因最后一个窗口关闭而停机，`Program.Main` 的停机兜底（`CloseAllAsync`，幂等）仍会跑一遍会话关闭。 |
| 30 | **（R5）`Program.Main` 的停机兜底与 C2 的关系** | 它是**进程级停机兜底**，不是 C2：直接逐个 `session.CloseAsync()`，不先关 Binding、不等编排任务。正常路径中 C2 已在窗口关闭前完成，该兜底只剩异常退出场景。其绕过编排的边界如实记录，不在本轮改写。 |

## Consequences

**正面**

- Detach 与销毁解耦：标签切换/重新挂载不会误关 Binding 或 Workspace，重新挂载可恢复绘制。
- 永久销毁只影响该视口，工作区可继续存活并由新 Binding 重新挂载。
- Binding 创建者唯一、销毁入口唯一；不依赖控件析构或可视树事件的偶然顺序。
- F-1 屏障与 WP-30A 读者契约保持不变。
- 关闭保证措辞与真实时序一致，不再暗示"零 post-close 绘制"。
- **C1 / C2 分离**：关闭一个工作区不会再连带关闭兄弟工作区、会话或包级资源；项目关闭则明确要求
  先关全部 Binding，避免在 presentation 仍活动时释放 `AssetStore` / `Database`。
- **C2 单一编排入口**：正常命令与窗口关闭（`Closing` 取消-编排-重关）汇聚到同一方法，清理逻辑
  只有一份；不会出现"命令路径关了一半、事件路径又关一遍"或两条路径互相绕过的情形。
- **关闭顺序由依赖方向固定**：presentation → Application → 窗口，与"谁依赖谁"一致，而不是由
  触发点的调用先后偶然决定。

**代价 / 约束**

- 拥有者必须显式调用 `binding.CloseAsync()`；资源销毁只发生在编排方法内部，可取消的 `Closing`
  中**永不**直接销毁（R5：`Closing` 只做取消-编排-重关的流程控制）。这是有意的：`Closing` 被
  取消时不能留下已销毁的视口。
- 暂时 Detach 期间 Runtime、Publisher 与最后快照继续驻留；这是恢复绘制的代价，M1 可接受。
- 已关闭 Binding 不可复用；重新显示必须新建 Binding + Runtime，旧控件必须丢弃。
- M1 不能在一个工作区开多个地图视口；UI 不应提供该入口。
- 组合根需提供可对同一 Workspace 再次调用的 Binding 工厂方法（旧 Binding 关闭后）。
- **两条路径必须分别实现**：C1 与 C2 不共用最后一公里（一个调 `CloseWorkspaceAsync`，一个调
  `CloseAsync`），共用错一步就是"关标签页 = 关项目"。C2 还需要一个"遍历全部存活 Binding"的
  显式步骤，不能依赖选中项。
- **既有缺口带入实现期**：项目关闭时子工作区的 `DisposeAsync()` 未被调用（Open Questions 第 6 条）。在本 ADR
  约束下，接线只能如实反映现状；补齐该缺口属 Application 代码变更，需另开实现任务。
- **C2 编排需要一个状态位与一个存活集合**：`CloseProjectLifetimeAsync` 必须记住"已启动"（否则
  `Closed` 与命令会各跑一遍），项目级还要维护可枚举的存活 Binding 集合。两者都是最小具体实现，
  但不是零成本——它们属于 Phase 3 接线范围。
- **现有命令体必须改写**：`CloseProjectAsync` 目前是 `Session.CloseAsync()` 后才 `ProjectClosed`，
  R4 要求在它前面插入 Binding 关闭并把它改为委托给编排方法。这是 ADR 明确要求改变的一处既有顺序
  （仅 Desktop ViewModel 层，不改 Application）。
- **窗口关闭经 `Closing` 异步完成后重关（R5）**：`ProjectWindow` 可能是最后一个窗口（停机模式
  `OnLastWindowClose` 已核实），`Closed` 之后的 fire-and-forget 编排会随消息循环退出而中断。
  窗口路径改为"`Closing` 取消本次关闭 → 接入同一编排 → await 完成后再次关闭"，编排完成责任
  由处理器承担；正常命令路径不变。代价是窗口关闭被推迟到 C2 完成（用户可能感知短暂延迟），
  且 `Closing` 处理器是 `async void`，异常必须就地观察（§8B 第 26/29 条）。

## Rejected Alternatives

- **Detach 即关闭（R1 原方案）**：同一控件重新挂载后 Binding 已终态，无法恢复绘制；还会把标签切换误判为销毁。已废弃。
- **Detach 时释放 Runtime，Attach 时新建**：同一视口出现两套 generation/publisher 生命周期，切换标签即丢失请求连续性；所有权在控件与拥有者之间摇摆。
- **在 `Window.Closing` 执行销毁**：`Closing` 可被其他处理器 `Cancel`，会留下已销毁但窗口仍开的状态。资源释放只发生在编排方法内部。R5 的"取消本次关闭 + 异步完成编排 + 再次关闭"**不属于**此项——它不在 `Closing` 中销毁任何资源（见 §8B 第 28 条）。
- **假定应用生命周期会等待 C2（R5 否决的方案 A）**：反射核实默认停机模式为
  `OnLastWindowClose`，`StartWindow` 无阻止先关的守卫，`ProjectWindow` 可能是最后一个窗口；
  `Closed` 之后 fire-and-forget 的编排延续随消息循环退出永不执行。`Program.Main` 的停机兜底
  直接调 `session.CloseAsync()`、绕过编排且不等 C2 任务，不构成方案 A 要求的"等待关闭任务
  完成的应用级所有者"。已改用方案 B（§7.2 窗口关闭流程）。
- **只靠 `Window.Closed`，不提供显式关闭视口命令**：无法支持"关视口但保留工作区"（B）；B 必须可由拥有者显式触发。
- **让 `MapViewportControl.Dispose()` 关闭 Workspace 或 Dispose Binding**：视口销毁会误关工作区；且把资源释放交给可视树析构的偶然顺序。
- **通用生命周期管理器 / 事件总线**：把两个事件与一个显式命令能解决的事抽象成框架，违反最小机制。
- **等待所有在飞帧完成后再关闭**：UI 线程等待渲染，且 Avalonia 未提供该同步契约。
- **把 runtime 做成 DI Singleton / 全局注册表**：违反每视口私有与"无全局状态"。
- **先释放 Publisher 再关 Coordinator**：颠倒 F-1 契约。
- **多视口共享 runtime 或相机**：generation/序号/快照互相覆盖，ViewState 是工作区级可变状态。
- **单工作区关闭复用 `ProjectSession.CloseAsync()`（R2 文本的隐含方案）**：会关闭全部兄弟工作区
  并释放 `AssetStore` / `Database`，即"关一个标签页 = 关整个项目"；与 `CloseWorkspaceAsync` 的
  作用域语义直接冲突。已由 R3 拆为 C1 / C2 废弃。
- **C2 只关闭当前选中 Binding 就调用 `CloseAsync()`**：其余视口的 Coordinator/Publisher 仍活动，
  包级资源已被释放，造成在飞请求访问已释放对象。必须先遍历关闭全部 Binding。
- **在 ADR 中假定项目关闭已释放子工作区 CTS**：当前 `ProjectSession.CloseAsync` 不调用子工作区
  `DisposeAsync`。把它写成"已释放"是虚构；正确做法是记录缺口并留给实现阶段。
- **在 `Window.Closed` 里写第二套清理逻辑（直接 `Session.CloseAsync()` + Dispose Binding）**：
  绕开"先关全部 Binding"的顺序，且与命令路径重复释放；两条路径必然漂移。
- **保留现有顺序（先 `Session.CloseAsync()`，再关窗口/视口）**：接入视口后等于在 presentation
  仍活动时释放包级资源；`Closed` 再补关 Binding 只会更晚，且会读到已释放的 Publisher。
- **只用 `binding.CloseAsync()` 的幂等性来"保证" C2 幂等**：单个 Binding 幂等不等于整条编排幂等，
  第二次调用仍会重跑 `ProjectSession.CloseAsync()` 与资源释放。
- **用一个"已关闭"布尔量让后续调用直接 return**：首次尚未完成时直接返回会让 `Closed` 路径提前
  结束，调用方以为项目已关闭；必须等待首次操作而不是跳过。
- **为幂等引入异步锁 / SemaphoreSlim / 通用关闭服务 / 调度器**：一个私有字段记忆首次调用即可满足
  契约，额外同步原语违反最小机制，且会诱使 UI 线程等待。
- **在 `Window.Closed` 处理器里 `await` 完整编排再"确认"关闭（R4 版兜底，R5 废弃其原始形态）**：
  `Closed` 触发时窗口已关，若是最后一个窗口则停机已开始，事件处理器的 `await` 延续可能永不执行。
  R5 把等待移到 `Closing`——先取消本次关闭，窗口保持存活，`await` 才有意义（§7.2）。

## Implementation Constraints

1. 不修改 `ProjectSession`、`MapWorkspaceSession`、`ProjectWorkspaceViewModel`、`ViewportPresentationRuntime`、`PresentationRequestCoordinator`、`SceneSnapshotPublisher` 的既有公共 API 与语义。**唯一例外**：按第 14 条改写 `ProjectWorkspaceViewModel.CloseProjectAsync` 的**方法体**（委托给 `CloseProjectLifetimeAsync`），命令名、签名与对外可观察结果（项目被关闭、窗口关闭）不变。
2. 不删除/放宽既有测试（含 `CoordinatorClosePublicationRaceTests`）。
3. `DetachedFromVisualTree` **不得**调用 `RequestClose()` / `CloseAsync()` / `DisposeAsync()`；只置控件自身 `_isAttached`。
4. `AttachedToVisualTree` 必须检查 `binding.IsClosed`：为 true 时拒绝绘制并报诊断，不得静默重开。
5. `binding.CloseAsync()` 只由拥有者在 B / C1 / C2 命令中调用；必须幂等；`Window.Closed` 可作兜底但不得成为第二套逻辑。
6. 不在 `Window.Closing` 释放任何资源。`Closing` 只允许承担 R5 的流程控制（取消本次关闭、
   启动/接入唯一编排、编排完成并观察异常后再次请求关闭）；任何 `using` / `Dispose` /
   `binding.CloseAsync()` 调用都不得出现在处理器内。
7. Binding 构造时创建 Runtime 与 Renderer；工厂只创建 Binding。禁止其他代码创建/替换 Runtime。
8. `MapDrawOperation.Render` 入口先检查 `IsClosed`，再获取 lease；保证边界按 §5，不得写成"零 post-close 绘制"。
9. **C1 路径禁止出现 `ProjectSession.CloseAsync()`**：只允许 `CloseWorkspaceAsync(workspaceId)`，
   随后 `Workspaces.Remove` 与 `workspace.DisposeAsync()`；不得遍历或关闭兄弟 Binding。接线时
   必须在既有 ViewModel 三连（`CloseWorkspaceAsync` → `Remove` → `DisposeAsync`）**之前**插入
   `binding.CloseAsync()`（§7.1 步骤 1），既有三连的相对顺序不得改动。
10. **C2 路径必须先遍历关闭全部存活 Binding，再调用 `ProjectSession.CloseAsync()`**：遍历依据是
    该会话下的工作区集合，不是当前选中项；单个 Binding 的子序必须与 §7.1 步骤 1 完全一致。
11. 窗口关闭只接入 C2 的编排方法（`Closing` 流程，见第 15 条）；不得把它接到 C1，也不得在
    单工作区 Tab 关闭时触发。
12. 不得在文档或实现中把"项目关闭时子工作区 `DisposeAsync` 已调用"当作既有事实；该缺口
    （Open Questions 第 6 条）在补齐前必须如实标注。
13. **C2 唯一的编排方法是 `ProjectWorkspaceViewModel.CloseProjectLifetimeAsync()`**：不得在
    `ProjectWindow` 或其它位置再实现一份"关 Binding + 关会话"的组合；`CloseProjectAsync` 与
    窗口关闭（`Closing` 流程）都只调用它。
14. `CloseProjectAsync` 的命令体必须改为委托给编排方法；**不得**保留 `Session.CloseAsync()` 在
    Binding 关闭之前执行的顺序（这是 R4 要求改变的唯一既有顺序）。
15. **窗口关闭入口是 `Window.Closing` 的取消-编排-重关流程（R5）**：
    - 编排已完成 → 直接放行，不取消；
    - 编排未启动或进行中 → `Cancel = true`，启动或接入 `CloseProjectLifetimeAsync()`，
      `await` 完成（异常就地观察：记诊断并仍放行关闭，不留未观察异常、不留关不掉的窗口），
      然后再次 `Close()`；
    - 不得在第一次 `Closing` 中提前释放任何资源；重复关闭请求只等待同一套 C2；
    - 不为 `Closed` 新增编排接线；`Program.Main` 的停机兜底保持原样，不改写。
16. 编排方法必须可安全重复调用：第二次调用等待首次操作的完成状态，不并行、不跳过；实现用
    最小手段（私有字段）即可，禁止为此引入锁服务、异步锁、调度器或生命周期管理器。
17. 项目级存活 Binding 集合只用于 C2 枚举；不得把它做成全局注册表、DI Singleton 或公共 API，
    也不得让它承担创建/释放职责。
18. 启动后不得中途放弃：`CancellationToken` 只用于启动前拒绝，Binding 关闭阶段使用
    `CancellationToken.None`；不得出现"Binding 已关、会话未关"的半关闭状态。
19. 不触碰 `docs/map-editor/implementation/`；本阶段只写文档。

## Open Questions

1. 当前 Desktop 外壳是否真的会在标签切换时触发 Detach/Attach，需在实现期用真实窗口验证；若某些布局不触发，A 仍按"保留"处理，不影响契约。
2. ~~`Window.Closed` 与关闭项目命令的完成归属~~ **R5 已裁决，不再是未决问题**：完成责任已定——
   命令路径由 `CloseProjectAsync` 调用方 `await`；窗口路径由 `Closing` 处理器取消本次关闭后
   `await` 同一编排、完成后重关窗口（§7.2 窗口关闭流程、§8B 第 26 条）。仍属实现期观察项的
   只剩 UI 呈现：编排进行中是否显示"正在关闭"状态（可并入第 10 条的产品决定）。
3. 暂时 Detach 期间是否需要在 UI 上显示"已暂停"状态，属产品决定；不影响 Binding 保留策略。
4. 多视口需求若出现，必须先拆分每视口相机状态并另立 ADR；本条不预先设计。
5. 在飞帧的诊断计数（close 后完成帧数）是否具名记录未定；仅观测，不改变 §5 边界。
6. **既有生命周期待核实项（不在本轮修复）**：`ProjectSession.CloseAsync()` 关闭子工作区时只调用
   `workspace.CloseAsync()`，未调用 `workspace.DisposeAsync()`（`ProjectSession.cs:223-239`）；
   `ProjectSession.DisposeAsync()` 在 `Closing`/`Closed` 时直接返回，也不会补上。因此 C2 路径上
   `MapWorkspaceSession._derivedWorkCts` 未被释放。补齐需要修改 Application 代码（例如在
   `CloseAsync` 的子工作区循环里追加 `DisposeAsync`，或让 `MapWorkspaceSession.CloseAsync`
   自身承担释放），属实现阶段任务，须单独裁决并补测试（重复关闭、关闭后 Dispose 的幂等性）。
7. `ProjectWindow` 当前**没有** `Closing` / `Closed` 处理器，会话关闭由 `CloseProjectAsync`
   → `ProjectClosed` → `Window.Close` 驱动（`ProjectWindow.axaml.cs:9-40`）。R5 之后窗口入口是
   `Closing` 的取消-编排-重关流程；剩余为纯接线细节：`Closing` 处理器的订阅位置（构造函数随
   `DataContext` 变化重新挂接，与现有 `ProjectClosed` 的订阅方式一致，还是 View 固定生命周期点）、
   "编排已完成"状态的判断载体（编排方法的返回值还是 ViewModel 上的最小状态位）、以及编排步骤 4
   的 `ProjectClosed → Window.Close()` 与处理器"再次关闭"的重入合并方式，需在实现期确认。
   这三条都不改变 §7.2 的完成责任归属。
8. 若未来出现"一个窗口只承载一个 Workspace"的外壳，其关闭仍按 C2 处理（先关全部 Binding 再关
   会话）；是否需要为该特例引入更短的路径，需新 ADR，不得在本 ADR 中推广为通用规则。
9. 单个 Binding 关闭失败时的诊断呈现方式未定（日志 / 状态文本 / 汇总）；契约只要求"不终止编排"，
   呈现形式属实现期决定。
10. 关闭编排期间是否需要在 UI 上禁用"关闭项目"命令（避免用户重复点击造成困惑），属产品决定；
    幂等契约已保证重复点击不会重复释放。
