# ADR WP-30B-10 — Workspace / Viewport 生命周期接线

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
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
| `ViewportPresentationRuntime` | Desktop 组合根的 workspace 工厂（`ViewportPresentationRuntime.Create()`） | `MapViewportControl`（per viewport 私有字段） | 视口关闭/工作区关闭时 `DisposeAsync()` | `IMPLEMENTATION_SUMMARY.md` §7；`ViewportPresentationRuntime.cs:30-35` |
| `MapViewportControl` | Desktop 窗口/视图层（工作区打开后创建） | 所在 `Window` 的可视树 | `DetachedFromVisualTree` / window `Closing` | ADR-07；Avalonia 12.1.6 `Visual.DetachedFromVisualTree`、`Window.Closing` |
| `Renderer`（Desktop 内部绘制对象） | `MapViewportControl` 构造时 new，`internal sealed` | 该控件私有字段 | `Dispose()`（M1 幂等空操作） | ADR-07 §决策；ADR-09 §8/§9 |
| `Camera`（M1 无独立类型） | 数值由 `WorkspaceViewState` 初始化 | `WorkspaceViewState`（Application 瞬态） | 随工作区；视口只持有不可变复制值 | ADR-07 问题 3；ADR-11 |
| Skia 资源 | draw 回调内（除 Avalonia 借出的 canvas/surface） | 本次回调 | 本次回调 `using` | ADR-09 |

### Workspace 与 Viewport 的关系

- **M1 采用 1:1**：一个 `MapWorkspaceSession` 至多挂载一个 `MapViewportControl`，因此也只有一个
  `ViewportPresentationRuntime` / generation 空间 / 帧相机复制。
- 原因：现有 `WorkspaceViewState` 属于**工作区**而非视口（相机、工具、选择都挂在它上面）；
  若允许多个视口共享同一工作区，就会共享相机状态并产生互相覆盖。这不是永久禁令：若将来需要
  多视口，必须先把每视口相机/工具状态从 `WorkspaceViewState` 拆出，再用新 ADR 修订本条。
- `ViewportGeneration`、`PresentationRequestSequence`、`SnapshotVersion` 都随 runtime 私有，
  绝不跨视口共享（`SNAPSHOT_LIFECYCLE.md` §8）。

### 关闭顺序（有依据的全序）

```
UI 线程：window Closing / viewport Detach
  1. 控件置 disposed 标志；停止新的请求与 InvalidateVisual()        （ADR-08 防迟到绘制）
  2. ViewportPresentationRuntime.DisposeAsync()
       2a. Coordinator.DisposeAsync()
             - CloseAsync：停新请求、取消活动请求
             - 在 publisher gate 内置位关闭屏障（F-1 线性化点）
       2b. Publisher.Dispose()：拒绝一切发布与版本分配；Current 不清空
  3. Renderer.Dispose()（幂等空操作；不触碰飞行中帧的局部资源）
  4. ProjectSession.CloseWorkspaceAsync(workspaceId)
       → MapWorkspaceSession.CloseAsync()：取消 _derivedWorkCts
       → 从 ProjectSession._workspaces 移除
  5. MapWorkspaceSession.DisposeAsync()（释放 _derivedWorkCts）
  6. ProjectSession.CloseAsync()：关闭其余工作区 → AssetStore → Database
```

**依据说明**：屏障必须先于 publisher 释放（步骤 2a→2b）是 WP-30A/Phase 1 的既有契约；
runtime 必须先于 workspace 关闭（步骤 2→4）是因为 presentation 请求的取消/迟到拒绝由
coordinator 独立完成，不能依赖 workspace 的 `_derivedWorkCts` 来"顺带"保证正确性；
Renderer 放在最后是因为 M1 没有需要抢在 publisher 之前释放的 Skia 资源（ADR-09）。

### 十个必答题

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 谁创建 Workspace | `ProjectSession.OpenWorkspaceAsync`（Application），经 Core Domain 解析上下文后构造；Desktop 不自行 new。 |
| 2 | 谁创建 MapViewportControl | Desktop 窗口/视图层，在工作区已存在且 runtime 已由组合根工厂创建后创建；构造时接收 session + runtime。 |
| 3 | 谁创建 Renderer | `MapViewportControl` 自己创建 `internal sealed` 绘制对象作为私有字段；不注册 DI、不暴露接口。 |
| 4 | Workspace 和 Viewport 是什么关系 | 组合关系：Viewport 是 Workspace 的**可视宿主**；Viewport 依赖 Workspace 的上下文与 ViewState，Workspace 不依赖 Viewport 的存在（会话可无视图存活）。M1 为 1:1。 |
| 5 | 一个 Workspace 是否允许多个 Viewport | **M1 不允许**（理由见上）；将来拆分每视口状态后可重新裁决。多个工作区/窗口仍可并存且 runtime 彼此隔离。 |
| 6 | Viewport 销毁是否意味着 Workspace 销毁 | **否**。控件分离只关闭呈现管线；工作区会话仍归 Application 生命周期管理，由显式的"关闭工作区 / 关闭项目"命令结束。 |
| 7 | Workspace 关闭时如何处理未完成 Presentation Request | `Coordinator.CloseAsync()` 停新请求、取消活动请求、在 publisher 锁内置屏障；其后任何完成路径（包括发布器未释放窗口内的迟到完成）一律 `RejectedClosed`。workspace 的 `_derivedWorkCts` 取消是额外一层，不承担正确性。 |
| 8 | Renderer 释放早于或晚于 Publisher | M1 Renderer 无资源，`Dispose()` 幂等空操作；按全序列在 publisher 释放**之后**。不允许把 Skia 资源放进 Renderer 以改变这一顺序（ADR-09）。 |
| 9 | 如何保证关闭之后没有迟到绘制 | 两道门：① UI 侧 disposed 标志一旦置位，不再有新的 `InvalidateVisual()` / draw operation；② F-1 屏障拒绝关闭后的发布。已在飞行中的帧允许自然完成，但只消费已捕获的快照与局部资源，不读已释放对象。不承诺"打断正在执行的 GPU 命令"。 |
| 10 | 如何保证绘制过程中 ViewState 一致 | 只有 UI 线程写 `WorkspaceViewState`（ADR-08）；控件在其 `Render(DrawingContext)` 中同步读取并复制成不可变帧输入（ADR-11）；Renderer 与 draw operation 都不回读可变对象；正式快照由 publisher 原子交换，一帧一引用。 |

## Consequences

**正面**

- 关闭全序来自现有代码与 F-1 契约，不依赖偶然的 GC/Dispose 顺序。
- 视口的销毁不会误伤工作区/项目会话；兄弟工作区继续独立运行。
- 每视口私有 runtime 保持 WP-30A 的隔离语义与诊断计数。
- 飞行中帧无共享资源，关闭时无需等待渲染完成。

**代价 / 约束**

- M1 不能在一个工作区开多个地图视口；这是显式限制，需在 UI 上避免提供该入口。
- 视图层关闭与工作区关闭必须由同一 UI 命令编排，不能分散到控件析构中隐式执行。
- 组合根需要在 workspace 工厂里创建 runtime 并把它交给视口；这属于实现期的接线工作。

## Rejected Alternatives

- **让 `MapViewportControl.Dispose()` 直接 `MapWorkspaceSession.CloseAsync()`**：视口关闭会误关工作区，破坏 Application 生命周期。
- **把 runtime 做成 DI Singleton / 全局注册表**：直接违反每视口私有与"无全局状态"纪律。
- **先释放 Publisher 再关 Coordinator**：颠倒既有契约；F-1 屏障必须先置位。
- **等待所有在飞帧完成后再关闭**：会让 UI 线程等待渲染，且 Avalonia 未提供该同步契约。
- **多视口共享一个 runtime 或共享相机**：generation/序号/快照会互相覆盖，且 ViewState 是工作区级可变状态。

## Implementation Constraints

1. 不修改 `ProjectSession`、`MapWorkspaceSession`、`ProjectWorkspaceViewModel`、`ViewportPresentationRuntime`、
   `PresentationRequestCoordinator`、`SceneSnapshotPublisher` 的既有公共 API 与语义。
2. 不删除/放宽既有测试（含 `CoordinatorClosePublicationRaceTests`）。
3. `DisposeAsync()` 只在 UI 生命周期命令中调用一次；幂等性由既有实现保证，不另加重置逻辑。
4. 视口控件必须在 `DetachedFromVisualTree` / `Closing` 上置 disposed 标志，且不从渲染回调调用关闭逻辑。
5. 不触碰 `docs/map-editor/implementation/`；本阶段只写文档。

## Open Questions

1. 组合根中 runtime 的具体持有位置（工厂闭包变量 vs ViewModel 字段）在实现期确定；本 ADR 只要求 per-workspace 唯一且交给视口。
2. 用户直接点击窗口关闭按钮（而非"关闭工作区"命令）时的会话关闭入口尚未接线；实现期需明确由 window `Closing` 触发同一全序，而不是只关窗口。
3. 多视口需求是否会在 WP-30C 之前出现；若出现必须先处理每视口相机状态拆分。
4. 飞行中帧的诊断计数（close 后完成帧数）是否具名记录未定。
