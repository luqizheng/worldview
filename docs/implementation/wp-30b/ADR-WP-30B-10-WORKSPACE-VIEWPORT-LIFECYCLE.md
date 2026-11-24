# ADR WP-30B-10 — Workspace / Viewport 生命周期接线

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— `MapViewportBinding` 成为 Runtime / Renderer / 关闭标志的唯一持有者与关闭命令执行者；控件只转发信号。关闭后不再启动新绘制。
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`
- **相关**: `ADR-WP-30B-07`、`ADR-WP-30B-08`、`ADR-WP-30B-09`、`ADR-WP-30B-11`

## Context

现有代码已定义应用侧生命周期，但尚未接入视口：

- `ProjectSession.OpenWorkspaceAsync` 创建 `MapWorkspaceSession` 并加入 `_workspaces`；
  `CloseWorkspaceAsync` 关闭单个工作区并移除；`CloseAsync` 先关全部子工作区，再逆序释放
  Asset Store → Database（`WorldView.Application/Sessions/ProjectSession.cs:144-253`）。
- `MapWorkspaceSession` 构造时创建 `WorkspaceViewState`，`CloseAsync()` 置 `_closed` 并取消
  `_derivedWorkCts`，`DisposeAsync()` 释放该 CTS（`MapWorkspaceSession.cs:20-26,82-102`）。
- `ProjectWorkspaceViewModel` 持有 `Workspaces` 集合，`CloseWorkspaceAsync` 命令依次为
  `Session.CloseWorkspaceAsync` → `Workspaces.Remove` → `workspace.DisposeAsync()`
  （`ProjectWorkspaceViewModel.cs:131-145`）。
- WP-30A 交接要求：在组合根的 workspace 工厂内为每个 `MapWorkspaceSession` 创建
  `ViewportPresentationRuntime`，关闭时 `DisposeAsync`（`IMPLEMENTATION_SUMMARY.md` §7）。
- `ViewportPresentationRuntime.DisposeAsync()` **先** `Coordinator.DisposeAsync()`（其中
  `CloseAsync()` 在发布器锁内置位 F-1 屏障），**后** `Publisher.Dispose()`；顺序即契约
  （`ViewportPresentationRuntime.cs:56-60`；`PresentationRequestCoordinator.cs:393-418`；
  `SNAPSHOT_LIFECYCLE.md` §5.1/§7）。

ADR-07 R1 进一步引入 Desktop 内部具体类 `MapViewportBinding`：由组合根每工作区创建，持有
Runtime、Renderer、generation 与 `volatile bool IsClosed`，并作为**唯一的关闭命令执行者**；
`MapViewportControl` 只持有它的一个引用并转发 UI 生命周期信号。

本 ADR 只规定这些对象之间"谁创建谁、谁持有谁、按什么顺序释放"，不实现接线。

## Decision Drivers

1. 保留 WP-30A 的每次视口独立 runtime 与 F-1 关闭屏障，不为视口销毁改写 Publisher 语义。
2. 工作区（Application 会话）与视口（Desktop 控件）是不同生命周期，不能互相隐式销毁。
3. 关闭顺序必须有明确依据，不能依赖 `Dispose` 偶然调用顺序。
4. 不引入全局注册表、事件总线或跨视口协调服务。
5. 单写者 UI 线程 + 不可变帧输入保证绘制期间 ViewState 一致（ADR-08/ADR-11）。

## Decision

### 对象创建 / 持有 / 释放总表

| 对象 | 创建者 | 持有者 | 释放触发 | 依据 |
|---|---|---|---|---|
| `ProjectSession` | `OpenProjectUseCase`（组合根注册） | `ProjectSessionRegistry` + `ProjectWorkspaceViewModel` | `CloseAsync` / `DisposeAsync`（Program 生命周期结束后统一编排） | `Program.cs:37-45`；`ProjectSession.cs:208-264` |
| `MapWorkspaceSession` | `ProjectSession.OpenWorkspaceAsync` | `ProjectSession._workspaces` + `ViewModel.Workspaces` | `ProjectSession.CloseWorkspaceAsync` → `MapWorkspaceSession.CloseAsync` → `DisposeAsync` | `ProjectSession.cs:168-179,183-202`；`ProjectWorkspaceViewModel.cs:131-145` |
| `WorkspaceViewState` | `MapWorkspaceSession` 构造函数 | `MapWorkspaceSession.ViewState` | 随工作区结束（无独立 Dispose） | `MapWorkspaceSession.cs:25,38,109-125` |
| `ViewportPresentationRuntime` | Desktop 组合根的 workspace 工厂（`ViewportPresentationRuntime.Create()`） | `MapViewportBinding`（per workspace 私有字段） | binding 在显式关闭命令中 `DisposeAsync()` | `IMPLEMENTATION_SUMMARY.md` §7；`ViewportPresentationRuntime.cs:30-35`；ADR-07 R1 |
| `MapViewportBinding` | Desktop 组合根的 workspace 工厂（与 Runtime 同时创建） | 控件只持有**一个引用**；binding 自身生命周期由显式关闭命令管理，不归可视树 | 显式关闭命令中 `RequestClose()` → 全序释放 | ADR-07 R1；本 ADR §关闭顺序 |
| `MapViewportControl` | Desktop 窗口/视图层（工作区与 binding 已存在后创建） | 所在 `Window` 的可视树 | `DetachedFromVisualTree` / window `Closing` → 只转发 `RequestClose()` | ADR-07；Avalonia 12.1.6 `Visual.DetachedFromVisualTree`、`Window.Closing` |
| `Renderer`（Desktop 内部绘制对象） | `MapViewportBinding` 构造时 new，`internal sealed` | `MapViewportBinding` 私有字段（**不是控件字段**） | binding 在 Publisher 释放后调 `Dispose()`（M1 幂等空操作） | ADR-07 R1；ADR-09 §8/§9；本 ADR §关闭顺序 |
| `Camera`（M1 无独立类型） | 数值由 `WorkspaceViewState` 初始化 | `WorkspaceViewState`（Application 瞬态） | 随工作区；视口只持有不可变复制值 | ADR-07 问题 3；ADR-11 |
| Skia 资源 | draw 回调内（除 Avalonia 借出的 canvas/surface） | 本次回调 | 本次回调 `using` | ADR-09 |

### Workspace 与 Viewport 的关系

- **M1 采用 1:1**：一个 `MapWorkspaceSession` 至多挂载一个 `MapViewportControl`，因此也只有一个
  `MapViewportBinding` / `ViewportPresentationRuntime` / generation 空间 / 帧相机复制。
- 原因：现有 `WorkspaceViewState` 属于**工作区**而非视口（相机、工具、选择都挂在它上面）；
  若允许多个视口共享同一工作区，就会共享相机状态并产生互相覆盖。这不是永久禁令：若将来需要
  多视口，必须先把每视口相机/工具状态从 `WorkspaceViewState` 拆出，再用新 ADR 修订本条。
- `ViewportGeneration`、`PresentationRequestSequence`、`SnapshotVersion` 都随 runtime 私有，
  绝不跨视口共享（`SNAPSHOT_LIFECYCLE.md` §8）。

### 关闭顺序（有依据的全序）

```
UI 线程：单一显式命令（window Closing / "关闭工作区"）
  1. 控件 Detach/Closing → 转发 binding.RequestClose()
       → binding 置 IsClosed = true（volatile）
       → 控件停止新的 InvalidateVisual()；binding 停止新的请求
       → draw operation 的 Render 入口检查该标志：关闭后不再启动绘制命令（ADR-08）
  2. binding.CloseAsync()
       2a. Coordinator.DisposeAsync()
             - CloseAsync：停新请求、取消活动请求
             - 在 publisher gate 内置位关闭屏障（F-1 线性化点）
       2b. Publisher.Dispose()：拒绝一切发布与版本分配；Current 不清空
       2c. Renderer.Dispose()（幂等空操作；不触碰飞行中帧的局部资源）
  3. ProjectSession.CloseWorkspaceAsync(workspaceId)
       → MapWorkspaceSession.CloseAsync()：取消 _derivedWorkCts
       → 从 ProjectSession._workspaces 移除
  4. MapWorkspaceSession.DisposeAsync()（释放 _derivedWorkCts）
  5. ProjectSession.CloseAsync()：关闭其余工作区 → AssetStore → Database
```

**依据说明**：
- 置 `IsClosed`（步骤 1）必须最早：它先于任何资源释放，使关闭后到达的 `Render` 调用在获取
  lease 之前返回；这是"关闭后不再启动绘制"的可验证判定点（ADR-08 问题 9）。
- 屏障必须先于 publisher 释放（步骤 2a→2b）是 WP-30A/Phase 1 的既有契约。
- Runtime 必须先于 workspace 关闭（步骤 2→3）是因为 presentation 请求的取消/迟到拒绝由
  coordinator 独立完成，不能依赖 workspace 的 `_derivedWorkCts` 来"顺带"保证正确性。
- Renderer 由 binding 持有，因此由 binding 在同一次显式关闭命令中释放（步骤 2c），且晚于
  Publisher（步骤 2b）——M1 没有需要抢在 publisher 之前释放的 Skia 资源（ADR-09）。
  控件析构**不**触发上述任何释放。

### 十个必答题

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 谁创建 Workspace | `ProjectSession.OpenWorkspaceAsync`（Application），经 Core Domain 解析上下文后构造；Desktop 不自行 new。 |
| 2 | 谁创建 MapViewportControl | Desktop 窗口/视图层，在工作区与 `MapViewportBinding` 均已由组合根创建后创建；构造/Attach 时接收 binding（binding 内部再引用 session 与 runtime）。控件不创建 binding。 |
| 3 | 谁创建 Renderer | `MapViewportBinding` 构造时创建 `internal sealed` 绘制对象作为私有字段；不注册 DI、不暴露接口、不归控件持有。 |
| 4 | Workspace 和 Viewport 是什么关系 | 组合关系：Viewport 是 Workspace 的**可视宿主**；binding 引用的上下文与 ViewState 来自 Workspace，Workspace 不依赖 Viewport 的存在（会话可无视图存活）。M1 为 1:1。 |
| 5 | 一个 Workspace 是否允许多个 Viewport | **M1 不允许**（理由见上）；将来拆分每视口状态后可重新裁决。多个工作区/窗口仍可并存且 runtime 彼此隔离。 |
| 6 | Viewport 销毁是否意味着 Workspace 销毁 | **否**。控件分离只关闭呈现管线；工作区会话仍归 Application 生命周期管理，由显式的"关闭工作区 / 关闭项目"命令结束。 |
| 7 | Workspace 关闭时如何处理未完成 Presentation Request | `Coordinator.CloseAsync()` 停新请求、取消活动请求、在 publisher 锁内置屏障；其后任何完成路径（包括发布器未释放窗口内的迟到完成）一律 `RejectedClosed`。workspace 的 `_derivedWorkCts` 取消是额外一层，不承担正确性。 |
| 8 | Renderer 释放早于或晚于 Publisher | M1 Renderer 无资源，`Dispose()` 幂等空操作；由 binding 在同一次显式关闭命令中、在 publisher 释放**之后**调用（步骤 2b→2c）。不允许把 Skia 资源放进 Renderer 以改变这一顺序（ADR-09）。 |
| 9 | 如何保证关闭之后没有迟到绘制 | **三层，且保证边界必须明说**：① 控件 disposed 标志置位后不再有新的 `InvalidateVisual()` / draw operation；② `binding.IsClosed`（volatile）置位后，draw operation 的 `Render` 入口直接返回——**关闭后不再启动绘制命令、lease 获取或资源创建**；③ F-1 屏障拒绝关闭后的发布。**不承诺**打断已进入回调的帧：Avalonia 12.1.6 无公开取消契约，且 UI 不得等待渲染。该帧只消费已捕获的快照与局部资源，不读已释放对象。 |
| 10 | 如何保证绘制过程中 ViewState 一致 | 只有 UI 线程写 `WorkspaceViewState`（ADR-08）；控件 `Render(DrawingContext)` 调用 `binding.CaptureFrame()`，由 binding 同步读取并复制成不可变帧输入（ADR-11）；Renderer 与 draw operation 都不回读可变对象或控件；正式快照由 publisher 原子交换，一帧一引用。 |

## Consequences

**正面**

- 关闭全序来自现有代码与 F-1 契约，不依赖偶然的 GC/Dispose 顺序；R1 后由 binding 单一执行。
- 视口的销毁不会误伤工作区/项目会话；兄弟工作区继续独立运行。
- 控件不再持有 Runtime / Renderer，非 UI 职责可脱离窗口测试；关闭后启动绘制被入口检查挡住。
- 每视口私有 runtime 保持 WP-30A 的隔离语义与诊断计数。
- 飞行中帧无共享资源，关闭时无需等待渲染完成。

**代价 / 约束**

- M1 不能在一个工作区开多个地图视口；这是显式限制，需在 UI 上避免提供该入口。
- 视图层关闭与工作区关闭必须由同一 UI 命令编排，不能分散到控件析构中隐式执行。
- 组合根需要在 workspace 工厂里创建 runtime 并把它交给视口；这属于实现期的接线工作。

## Rejected Alternatives

- **让 `MapViewportControl.Dispose()` 直接 `MapWorkspaceSession.CloseAsync()` 或直接 `DisposeAsync()` Runtime / Renderer**：视口关闭会误关工作区，破坏 Application 生命周期；且资源释放改成由可视树析构偶发触发，失去单一执行者与确定全序。控件只转发 `RequestClose()`。
- **把 runtime 做成 DI Singleton / 全局注册表**：直接违反每视口私有与"无全局状态"纪律。
- **先释放 Publisher 再关 Coordinator**：颠倒既有契约；F-1 屏障必须先置位。
- **等待所有在飞帧完成后再关闭**：会让 UI 线程等待渲染，且 Avalonia 未提供该同步契约。
- **多视口共享一个 runtime 或共享相机**：generation/序号/快照会互相覆盖，且 ViewState 是工作区级可变状态。

## Implementation Constraints

1. 不修改 `ProjectSession`、`MapWorkspaceSession`、`ProjectWorkspaceViewModel`、`ViewportPresentationRuntime`、
   `PresentationRequestCoordinator`、`SceneSnapshotPublisher` 的既有公共 API 与语义。
2. 不删除/放宽既有测试（含 `CoordinatorClosePublicationRaceTests`）。
3. `binding.RequestClose()` / `CloseAsync()` 只在显式 UI 生命周期命令中调用一次；幂等性由既有实现保证，不另加重置逻辑。控件 `Detach` 只转发信号。
4. 视口控件必须在 `DetachedFromVisualTree` / `Closing` 上置自身 disposed 标志并转发给 binding；不从渲染回调调用关闭逻辑。binding 必须最先置 `volatile bool IsClosed`，且 draw operation 在 `Render` 入口检查它后再取 lease。
5. 不触碰 `docs/map-editor/implementation/`；本阶段只写文档。

## Open Questions

1. 组合根中 `MapViewportBinding` 的具体持有位置（工厂闭包变量 vs ViewModel 字段）在实现期确定；本 ADR 只要求 per-workspace 唯一、由它持有 runtime/renderer、并只把一个引用交给控件。
2. 用户直接点击窗口关闭按钮（而非"关闭工作区"命令）时的会话关闭入口尚未接线；实现期需明确由 window `Closing` 触发同一全序，而不是只关窗口。
3. 多视口需求是否会在 WP-30C 之前出现；若出现必须先处理每视口相机状态拆分。
4. 飞行中帧的诊断计数（close 后完成帧数）是否具名记录未定。
