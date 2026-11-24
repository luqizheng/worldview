# ADR WP-30B-08 — UI Thread / Render Execution 边界

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— draw operation 入口增加 volatile 关闭检查；明确新进入的绘制被抑制、在飞回调不中断。
- **修订**: R2（2026-11-24）— 修正过度表述：入口读到 true 才返回；已读到 false 的在飞帧可继续；不保证 Close 返回后零绘制调用。Detach 不置关闭标志。
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`
- **相关**: `ADR-WP-30B-07`（宿主方案）、`ADR-WP-30B-09`（Skia 资源）、`ADR-WP-30B-10`（关闭顺序）、`ADR-WP-30B-11`（不可变帧输入）

## Context

WP-30A 明确了发布端锁序（coordinator gate → publisher gate）、读者无锁
（`Volatile.Read` / `Interlocked.Exchange`）与 F-1 关闭屏障，但**未定义真实渲染线程的纪律**：
M1 只有测试替身读者，不经历 Avalonia 的绘制回调。`docs/implementation/wp-30a-review/
WP-30B_READINESS.md` §5.1/§6 把"UI 线程 / 渲染线程边界"列为 WP-30B 必答题。

本 ADR 只依据当前依赖版本（Avalonia 12.1.6、SkiaSharp 3.119.4）与现有代码确定边界，
不假设组合器内部实现。

### 已核实的外部事实

| 事实 | 证据 |
|---|---|
| Avalonia 是单线程 UI 模型；控件创建、属性读写、布局、渲染、输入处理都在 UI 线程；从后台线程访问控件会抛 `InvalidOperationException("Call from invalid thread.")` | Avalonia 官方 [Threading model](https://docs.avaloniaui.net/docs/app-development/threading) |
| `Dispatcher.UIThread.Post/InvokeAsync` 可把工作送回 UI 线程；`AvaloniaObject.Dispatcher` 绑定对象创建线程 | 同上；本地 `Avalonia.Base.xml` `T:Avalonia.Threading.Dispatcher` |
| 渲染管线是 retained-mode：Render pass 构建 scene graph，compositor 再提交绘制；帧序为输入 → 属性变化 → 布局 → Render → 组合 | Avalonia 官方 [Architecture](https://docs.avaloniaui.net/docs/fundamentals/architecture) |
| `ICustomDrawOperation.Render(ImmediateDrawingContext)` 是通过 `DrawingContext.Custom` 进入的绘制回调；Skia 资源经 `ISkiaSharpApiLeaseFeature.Lease()` 取得并在回调内 `using` | 本地包反射 + 官方 [ICustomDrawOperation](https://docs.avaloniaui.net/api/avalonia/rendering/scenegraph/icustomdrawoperation) / [Native interop](https://docs.avaloniaui.net/docs/app-development/native-interop)（详见 ADR-07） |
| `SceneSnapshotPublisher.Current` 是纯 `Volatile.Read`；发布为 `Interlocked.Exchange`；渲染路径零锁、零等待 | `WorldView/src/WorldView.Presentation/Publication/SceneSnapshotPublisher.cs:46,146-169,179-229`；`docs/implementation/wp-30a/SNAPSHOT_LIFECYCLE.md` §2/§5.1 |
| 快照不可变且不含任何 Avalonia / Skia / ViewModel / session 引用 | `docs/implementation/wp-30a/ARCHITECTURE.md` §3；`docs/map-engine/PRA-01_PRESENTATION_CONTEXT_AND_IMMUTABLE_SCENE_SNAPSHOT_CONTRACT_v0.1.md` §4.2/§6.4 |
| `WorkspaceViewState` 当前是**可变类**：`CameraX/Y/Zoom/ActiveTool` 为 settable，`SelectedItemIds` 为可变 `HashSet<Guid>`；由 `MapWorkspaceSession` 构造并持有 | `WorldView/src/WorldView.Application/Sessions/MapWorkspaceSession.cs:109-125` |

**明确的不确定性**：官方文档保证"控件侧渲染/输入/布局在 UI 线程"，但没有把
`ICustomDrawOperation.Render` 所运行的 OS 线程作为公开契约固定下来；Avalonia 12.1.6 的
compositor 内部可能使用自己的线程。因此本 ADR **不声明**"绘制一定在 UI 线程"或"一定存在独立
Render Thread"，而是把绘制回调视为**Avalonia 拥有的执行上下文**，并让绘制代码对它不敏感。

## Decision Drivers

1. 优先使用 Avalonia 自身调度与绘制生命周期；不创建第二套线程系统。
2. 不为理论性能问题引入通用 Render Scheduler / Render Thread。
3. 渲染回调必须短、只读、无等待：PRA-01 §6.4/§6.5、§8。
4. 不允许跨线程共享 Skia 原生对象；快照不含 Skia。
5. UI 线程不得因为渲染而长时间阻塞；关闭要抑制**新进入**的绘制，但不等待、不承诺中断在飞帧。
6. 保留 WP-30A / WP-30B F-1 的关闭屏障语义，不在本阶段改写。
7. "关闭保证"必须按真实时序表述：入口命中 `IsClosed` 才返回，已通过入口检查的帧可继续；禁止使用暗示"零 post-close 绘制"的措辞。

## Decision

**不设独立 Render Thread；不设通用调度器；绘制只发生在 Avalonia 的 `ICustomDrawOperation.Render`
回调内，且入口先检查 `MapViewportBinding.IsClosed`（volatile）：读到 true 立即返回；读到 false
的帧允许继续（即使随后关闭置位）。所有控件与 ViewState 写入只在 UI 线程；快照用不可变引用
跨线程共享；Skia 对象绝不跨线程。**

### UI Thread 负责

| 操作 | 裁决 |
|---|---|
| 控件生命周期 | `AttachedToVisualTree` / `DetachedFromVisualTree`、`Loaded` / `Unloaded` 全部在 UI 线程处理。**Detach 只停止失效，不关闭 Binding、不释放 Runtime**；永久销毁由拥有者的显式命令或 `Window.Closed` 触发（ADR-10）。 |
| 输入事件 | 指针/键盘/焦点由 Avalonia 控件路由；M1 不实现 Pan/Zoom，但未来的相机交互也必须落在 UI 线程。 |
| Camera 交互 | 只在此线程修改 `WorkspaceViewState.CameraX/Y/Zoom`（现有可变类型）；不新增 Camera 写者。 |
| ViewState 更新 | 单写者原则：`MapWorkspaceSession.ViewState` 的所有写入必须来自 UI 线程；渲染路径只读其**复制值**。 |
| 控件尺寸变化 | `Control.SizeChanged` → UI 线程捕获新尺寸 → `ViewportGeneration.Advance()` + 重发请求 + `InvalidateVisual()`。 |
| DPI 变化 | `TopLevel.ScalingChanged` / `RenderScaling`（12.1.6 已核实）→ 同上；DPI 值进入不可变帧输入，不让 Renderer 回读控件。 |
| UI 状态更新 | 加载/空态指示在 Renderer 之外（ViewModel/控件状态），更新在 UI 线程。 |
| 快照捕获 | 在 UI 线程的 `Control.Render(DrawingContext)` 内调用 `binding.CaptureFrame()`；由 Binding 执行一次 `Publisher.Current` 并构造不可变帧输入；回调内不再回读 Publisher。 |

### Render Execution 负责

| 操作 | 裁决 |
|---|---|
| SceneSnapshot 捕获 | 不在回调内捕获（见上）；回调只消费 draw operation 携带的引用。即使未来改为回调内 `Volatile.Read`，也只能捕获一次并全程复用。 |
| Render Frame | 只调用 Desktop 内部无状态绘制对象，传入 `SKCanvas` 与不可变帧输入；不做分支外的状态积累。 |
| Skia Canvas 使用 | `ImmediateDrawingContext.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature))` → `feature.Lease()`；`lease.SkCanvas` 仅在本次 `Render` 调用栈内借用；lease 为 null 时跳过绘制。 |
| SKSurface / SKBitmap / SKPaint 等资源 | 仅在本回调内创建、使用并 `using` 销毁；M1 不缓存、不提升到字段、不放静态变量（ADR-09）。 |
| 资源释放 | 由回调内的 `using` 完成；不依赖 GC、不由 UI 线程"事后"释放回调内的对象。 |
| 窗口关闭 / 渲染中断 | 只有永久销毁/工作区关闭才由拥有者置 `binding.IsClosed = true`（volatile）并停止新请求；Detach 不置位。draw operation 在 `Render` 入口读该标志：true 则立即返回（不取 lease、不发命令）；false 则继续，之后即使关闭置位也不被打断。在飞帧用已捕获数据自然结束，不等待、不打断。 |

### 十个必答题

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 是否需要独立 Render Thread | **否**。无新线程、无 Thread/Task 常驻循环。绘制走 Avalonia 自己的 render pass。 |
| 2 | 是否允许后台线程绘制 | **否**。后台可做场景准备/装配（既有 coordinator 职责），但不得触碰 `SKCanvas` / `ImmediateDrawingContext` / Avalonia 控件。 |
| 3 | 是否需要 Dispatcher | **不新增自有 Dispatcher 抽象**。现成 `Avalonia.Threading.Dispatcher.UIThread` / `AvaloniaObject.Dispatcher` 是唯一回 UI 通道；当且仅当请求完成 continuation 不在 UI 线程时，用 `Post` 投递 `InvalidateVisual()`/状态更新。UI 线程发起的 `async` 默认经既有 `SynchronizationContext` 回到 UI 线程，不再额外包装。 |
| 4 | 是否需要锁 | **渲染路径零锁**。Publisher/Coordinator 内部既有 gate 不改；Renderer 无共享可变字段；可变 ViewState 由 UI 单写者保护；帧输入为值拷贝。 |
| 5 | 是否允许跨线程传递 SKSurface / SKCanvas | **禁止**。lease 与其提供的对象不离开回调栈；不缓存、不 `Post`、不放字段。 |
| 6 | SceneSnapshot 是否可跨线程共享 | **可以**（且只允许以此方式）：它真正不可变、无 Skia/Avalonia 引用；通过 `Volatile.Read` 捕获一次后可在回调线程安全读取。 |
| 7 | WorkspaceViewState 是否必须不可变 | **本阶段不改动其类型**（禁止修改 Application 业务代码）。纪律上等价于不可变：UI 线程外禁止读写；Renderer 只接收 ADR-11 定义的不可变帧输入。未来若改为 immutable record，是独立 ADR。 |
| 8 | 如何避免数据竞争 | 单写者（UI）+ 不可变帧输入 + 原子快照引用 + 无共享 Skia + 回调不读控件/ViewState。任何需要"当前相机"的绘制只能来自捕获值。 |
| 9 | 如何避免关闭窗口时迟到绘制 | 允许的交错：`Render enters → reads IsClosed == false → Close sets IsClosed = true → Close returns → 已进入的 Render 可继续`。保证：① 入口读到 true 时立即返回（不取 lease、不建 Skia 资源、不发命令）；② 置位前已读到 false 的在飞帧允许自然完成；③ **不保证** Close 返回后绝对没有绘制调用；④ **不保证**中断已进入的回调；⑤ 不等待在飞帧；⑥ UI 线程不等待渲染；⑦ 不加锁、不加信号量、不加帧屏障、不建独立 Render Thread。Detach 只停失效、不置 IsClosed；F-1 屏障另挡关闭后的发布（`SNAPSHOT_LIFECYCLE` §5.1）。完整边界见 ADR-10 §5。 |
| 10 | 如何确保 UI 线程不等待后台渲染完成 | 没有"后台渲染"这一环节；请求完成是异步回调，`InvalidateVisual()` 是入队而非同步绘制；`DisposeAsync()` 只关闭 coordinator + 释放 publisher 并置标志，**不**等待某帧画完，也不在回调里 `Wait`/`Invoke`。 |

## Consequences

**正面**

- 与 Avalonia 官方单线程 UI 模型和既有 `SynchronizationContext` 一致，避免跨线程控件访问异常。
- 渲染路径保持 WP-30A 的"零锁、零等待、零准备"读者契约。
- Skia 原生资源不跨线程，消除释放后使用与双重释放的主要来源。
- 不需要通用调度器、事件总线或全局状态。
- "关闭后绘制"有了可验证判定点：`Render` 入口的 volatile 检查，无需等待渲染排空。
- 保证边界与真实时序一致：不把"新帧被抑制"误写成"在飞帧被中断"，也不承诺零 post-close 调用。

**代价 / 约束**

- 不能从渲染回调读取最新相机/尺寸；看到的是上一次 UI 捕获的帧输入，这符合快照语义。
- 不能同步获取"绘制完成"信号；加载态与诊断需异步呈现。
- 若未来引入长期缓存的 Skia 资源，需重新裁决其创建/销毁线程，并用新 ADR 修订本文与 ADR-09。

## Rejected Alternatives

- **独立 Render Thread + 队列**：增加线程、队列、同步与关闭排空；M1 无实测收益；迟到绘制与资源释放更难证明。
- **后台 `SKSurface` 绘制 + `Dispatch` 贴位图**：跨线程传递原生资源、双缓冲所有权、DPI/窗口重建处理成本，且引入第二套显示通道。
- **渲染回调内读 `WorkspaceViewState` / Publisher 多次**：会读到半更新状态或混合版本，违反 PRA-01 §6.4。
- **用 `lock` 包住整个绘制**：违反 PRA-01 §6.5，且 UI 线程可能被渲染阻塞。

## Implementation Constraints

1. 渲染回调不得访问 Domain、数据库、文件系统、ViewModel、可变 ViewState 或任何 session。
2. `SKPaint` / `SKPath` / lease 等原生对象只在回调栈内创建与释放（ADR-09）。
3. 所有 Avalonia 对象操作在 UI 线程；后台完成路径如需更新控件，经 `Dispatcher.UIThread.Post`。
4. 禁止 `Task.Wait()` / `.Result` / `Dispatcher.Invoke` 出现在渲染回调或控件 `Render` 中。
5. 关闭顺序按 ADR-10；F-1 屏障与 publisher 释放顺序不得改写。
6. 本 ADR 不要求实现；实现期若发现操作系统线程与本文假设不符，以本文的"线程不敏感"约束为准，不修改契约。

## Open Questions

1. Avalonia 12.1.6 在不同平台/配置下 `ICustomDrawOperation.Render` 的实际 OS 线程尚未实测；实现期需用运行日志确认，但**不得据此放宽**上述纪律。
2. `InvalidateVisual()` 的合并时机与 generation 推进的对应关系需实测；若一次 UI 变化触发多次 render，帧输入必须仍然自洽。
3. 关闭时"飞行中帧"的可观察性：是否需要诊断计数（例如 close 后完成帧数）由实现期决定；该计数只用于观测，不改变"不等待渲染"的契约。
4. 若 Avalonia 未来公开回调取消或帧屏障 API，可重新裁决在飞帧的更强保证；在此之前以本 ADR 的边界定义为准。
4. 无 Skia feature（如无头测试）时的绘制降级与诊断位置未定。
